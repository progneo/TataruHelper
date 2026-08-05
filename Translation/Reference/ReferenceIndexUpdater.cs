using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;

using Microsoft.Extensions.Logging;

namespace Translation.Reference
{
    public enum ReferenceUpdateOutcome
    {
        AlreadyCurrent,
        Updated,
        Failed
    }

    public readonly struct ReferenceUpdateResult
    {
        public ReferenceUpdateResult(ReferenceUpdateOutcome outcome, string detail, int lines)
        {
            Outcome = outcome;
            Detail = detail ?? string.Empty;
            Lines = lines;
        }

        public ReferenceUpdateOutcome Outcome { get; }
        public string Detail { get; }
        public int Lines { get; }
    }

    /// <summary>
    /// Fetches the hand-made translation and rebuilds the index from it.
    ///
    /// The translation is a living project, so this exists to pick up its work
    /// without waiting for a release of the application. The export is around a
    /// gigabyte unpacked, which is why the archive is read as it downloads and
    /// nothing is ever written to disk but the finished index.
    /// </summary>
    public sealed class ReferenceIndexUpdater
    {
        private const string Repository = "xivrus/xiv_ru_weblate";
        private const string ArchiveUrl = "https://codeload.github.com/" + Repository + "/tar.gz/refs/heads/main";
        private const string RevisionUrl = "https://api.github.com/repos/" + Repository + "/commits/main";

        private static readonly Regex RevisionSha = new Regex("\"sha\"\\s*:\\s*\"([0-9a-f]{7,40})\"",
            RegexOptions.Compiled);

        private readonly ILogger _logger;

        public ReferenceIndexUpdater(ILogger logger)
        {
            _logger = logger;
        }

        /// <summary>
        /// The revision the translation project is currently at, or empty when
        /// it cannot be asked. Checked before downloading anything: the archive
        /// is hundreds of megabytes and most of the time it has not moved.
        /// </summary>
        public async Task<string> GetLatestRevisionAsync(CancellationToken cancellationToken)
        {
            try
            {
                using var client = CreateClient();
                var payload = await client.GetStringAsync(RevisionUrl, cancellationToken).ConfigureAwait(false);
                var match = RevisionSha.Match(payload);
                return match.Success ? match.Groups[1].Value : string.Empty;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", Convert.ToString(ex));
                return string.Empty;
            }
        }

        /// <summary>
        /// Rebuilds the index unless it already holds the current revision.
        /// </summary>
        /// <param name="progress">Bytes downloaded, and a line about the stage.</param>
        public async Task<ReferenceUpdateResult> UpdateAsync(
            string databasePath,
            string language,
            string currentRevision,
            IProgress<(long Bytes, string Stage)> progress,
            CancellationToken cancellationToken)
        {
            var latest = await GetLatestRevisionAsync(cancellationToken).ConfigureAwait(false);

            if (latest.Length > 0 && string.Equals(latest, currentRevision, StringComparison.OrdinalIgnoreCase))
            {
                return new ReferenceUpdateResult(ReferenceUpdateOutcome.AlreadyCurrent, latest, 0);
            }

            try
            {
                var builder = await DownloadAndBuildAsync(language, progress, cancellationToken)
                    .ConfigureAwait(false);

                if (builder.Lines.Count == 0)
                {
                    return new ReferenceUpdateResult(ReferenceUpdateOutcome.Failed,
                        "The export yielded nothing; its layout has probably changed.", 0);
                }

                progress?.Report((0, "Writing the index"));

                // Written beside the index and moved into place, so a download
                // that fails partway leaves the working index untouched.
                var temporaryPath = databasePath + ".new";
                Write(temporaryPath, builder, language, latest);
                File.Move(temporaryPath, databasePath, true);

                return new ReferenceUpdateResult(ReferenceUpdateOutcome.Updated, latest, builder.Lines.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", Convert.ToString(ex));
                return new ReferenceUpdateResult(ReferenceUpdateOutcome.Failed, ex.Message, 0);
            }
        }

        private async Task<ReferenceIndexBuilder> DownloadAndBuildAsync(
            string language,
            IProgress<(long Bytes, string Stage)> progress,
            CancellationToken cancellationToken)
        {
            var builder = new ReferenceIndexBuilder();
            var englishByFolder = new Dictionary<string, string>(StringComparer.Ordinal);
            var translatedByFolder = new Dictionary<string, string>(StringComparer.Ordinal);
            var translatedName = language + ".xlf";

            using var client = CreateClient();
            using var response = await client
                .GetAsync(ArchiveUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                .ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            await using var network = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            await using var counting = new CountingStream(network, progress);
            await using var decompressed = new GZipStream(counting, CompressionMode.Decompress);
            await using var archive = new TarReader(decompressed);

            var sheets = 0;
            while (await archive.GetNextEntryAsync(false, cancellationToken).ConfigureAwait(false)
                   is { } entry)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (entry.EntryType != TarEntryType.RegularFile || entry.DataStream == null)
                {
                    continue;
                }

                var name = entry.Name.Replace('\\', '/');
                var fileName = name.Substring(name.LastIndexOf('/') + 1);
                if (fileName != "en.xlf" && fileName != translatedName)
                {
                    continue;
                }

                var folder = name.Substring(0, Math.Max(0, name.Length - fileName.Length - 1));
                var content = await ReadAllAsync(entry.DataStream, cancellationToken).ConfigureAwait(false);

                // The two files of a sheet sit next to each other, so the one
                // that arrives first waits only for its partner.
                if (fileName == "en.xlf")
                {
                    if (translatedByFolder.Remove(folder, out var waitingTranslation))
                    {
                        builder.AddSheet(folder, content, waitingTranslation);
                        sheets++;
                    }
                    else
                    {
                        englishByFolder[folder] = content;
                    }
                }
                else
                {
                    if (englishByFolder.Remove(folder, out var waitingEnglish))
                    {
                        builder.AddSheet(folder, waitingEnglish, content);
                        sheets++;
                    }
                    else
                    {
                        translatedByFolder[folder] = content;
                    }
                }

                if (sheets % 200 == 0 && sheets > 0)
                {
                    progress?.Report((0, $"Read {sheets} sheets, {builder.Lines.Count} lines"));
                }
            }

            _logger?.LogInformation("Reference index rebuilt: {Sheets} sheets, {Lines} lines.",
                sheets, builder.Lines.Count);

            return builder;
        }

        private static void Write(string path, ReferenceIndexBuilder builder, string language, string revision)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate
            }.ToString());

            connection.Open();

            using (var schema = connection.CreateCommand())
            {
                schema.CommandText =
                    "PRAGMA journal_mode = OFF;" +
                    "CREATE TABLE line (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                    "CREATE TABLE pattern (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                    "CREATE TABLE speaker (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                    "CREATE TABLE gendered (source TEXT NOT NULL, feminine INTEGER NOT NULL, " +
                    "translated TEXT NOT NULL, PRIMARY KEY (feminine, source)) WITHOUT ROWID;" +
                    "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);";
                schema.ExecuteNonQuery();
            }

            using var transaction = connection.BeginTransaction();

            InsertPairs(connection, transaction, "line", builder.Lines);
            InsertPairs(connection, transaction, "pattern", builder.Patterns);
            InsertPairs(connection, transaction, "speaker", builder.Speakers);

            using (var gendered = connection.CreateCommand())
            {
                gendered.Transaction = transaction;
                gendered.CommandText = "INSERT OR IGNORE INTO gendered VALUES ($source, $feminine, $translated)";
                var source = gendered.CreateParameter();
                source.ParameterName = "$source";
                var feminine = gendered.CreateParameter();
                feminine.ParameterName = "$feminine";
                var translated = gendered.CreateParameter();
                translated.ParameterName = "$translated";
                gendered.Parameters.Add(source);
                gendered.Parameters.Add(feminine);
                gendered.Parameters.Add(translated);

                foreach (var entry in builder.Gendered)
                {
                    source.Value = entry.Key.Source;
                    feminine.Value = entry.Key.Feminine ? 1 : 0;
                    translated.Value = entry.Value;
                    gendered.ExecuteNonQuery();
                }
            }

            InsertPairs(connection, transaction, "meta", new Dictionary<string, string>
            {
                ["language"] = language,
                ["source"] = ArchiveUrl,
                ["revision"] = revision ?? string.Empty,
                ["lines"] = builder.Lines.Count.ToString(),
                ["patterns"] = builder.Patterns.Count.ToString(),
                ["speakers"] = builder.Speakers.Count.ToString(),
                ["gendered"] = (builder.Gendered.Count / 2).ToString(),
            });

            transaction.Commit();

            using var vacuum = connection.CreateCommand();
            vacuum.CommandText = "VACUUM";
            vacuum.ExecuteNonQuery();
        }

        private static void InsertPairs(
            SqliteConnection connection,
            SqliteTransaction transaction,
            string table,
            IEnumerable<KeyValuePair<string, string>> rows)
        {
            using var command = connection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = $"INSERT OR IGNORE INTO {table} VALUES ($key, $value)";

            var key = command.CreateParameter();
            key.ParameterName = "$key";
            var value = command.CreateParameter();
            value.ParameterName = "$value";
            command.Parameters.Add(key);
            command.Parameters.Add(value);

            foreach (var row in rows)
            {
                key.Value = row.Key;
                value.Value = row.Value;
                command.ExecuteNonQuery();
            }
        }

        private static async Task<string> ReadAllAsync(Stream stream, CancellationToken cancellationToken)
        {
            using var reader = new StreamReader(stream, new UTF8Encoding(false), true, 8192, true);
            return await reader.ReadToEndAsync(cancellationToken).ConfigureAwait(false);
        }

        private static HttpClient CreateClient()
        {
            var client = new HttpClient { Timeout = TimeSpan.FromMinutes(30) };
            // GitHub's API refuses anonymous requests without one.
            client.DefaultRequestHeaders.Add("User-Agent", "TataruHelper");
            client.DefaultRequestHeaders.Add("Accept", "application/vnd.github+json");
            return client;
        }

        /// <summary>Reports how much has come down, since the archive has no declared length.</summary>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _inner;
            private readonly IProgress<(long Bytes, string Stage)> _progress;
            private long _total;
            private long _lastReported;

            public CountingStream(Stream inner, IProgress<(long Bytes, string Stage)> progress)
            {
                _inner = inner;
                _progress = progress;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _inner.Read(buffer, offset, count);
                Advance(read);
                return read;
            }

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                Advance(read);
                return read;
            }

            private void Advance(int read)
            {
                _total += read;
                if (_total - _lastReported < 4 * 1024 * 1024)
                {
                    return;
                }

                _lastReported = _total;
                _progress?.Report((_total, "Downloading"));
            }

            public override bool CanRead => true;
            public override bool CanSeek => false;
            public override bool CanWrite => false;
            public override long Length => throw new NotSupportedException();

            public override long Position
            {
                get => _total;
                set => throw new NotSupportedException();
            }

            public override void Flush() => _inner.Flush();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

            protected override void Dispose(bool disposing)
            {
                if (disposing)
                {
                    _inner.Dispose();
                }

                base.Dispose(disposing);
            }
        }
    }
}
