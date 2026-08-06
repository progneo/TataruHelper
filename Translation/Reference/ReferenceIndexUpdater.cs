using System;
using System.Collections.Generic;
using System.Formats.Tar;
using System.Globalization;
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

    public enum ReferenceUpdateStage
    {
        /// <summary>
        /// The archive is coming down and being read at the same time, so one
        /// stage carries both how much has arrived and how much has been made
        /// of it. Reported as two stages, it read as though the application had
        /// begun installing an index it had not finished downloading.
        /// </summary>
        Downloading,

        /// <summary>The index is being written and put in place.</summary>
        Writing
    }

    /// <summary>
    /// Where an update has got to.
    ///
    /// Kept as numbers and a stage rather than a sentence: the sentence is
    /// shown to the user, and the user reads it in their own language, which
    /// this project knows nothing about.
    /// </summary>
    public readonly struct ReferenceUpdateProgress
    {
        public ReferenceUpdateProgress(ReferenceUpdateStage stage, long bytes, int sheets, int lines)
        {
            Stage = stage;
            Bytes = bytes;
            Sheets = sheets;
            Lines = lines;
        }

        public ReferenceUpdateStage Stage { get; }
        public long Bytes { get; }
        public int Sheets { get; }
        public int Lines { get; }
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
        /// <param name="progress">How far along, for the caller to phrase.</param>
        /// <param name="releaseIndex">
        /// Called once the new index is written and only the last step is left:
        /// the application holds the old file open, and Windows will not move
        /// anything over an open file. Left this late so the application is
        /// without its translations for the length of a rename rather than for
        /// the length of a download.
        /// </param>
        public async Task<ReferenceUpdateResult> UpdateAsync(
            string databasePath,
            string language,
            string currentRevision,
            IProgress<ReferenceUpdateProgress> progress,
            Action releaseIndex,
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

                progress?.Report(new ReferenceUpdateProgress(ReferenceUpdateStage.Writing, 0, 0, 0));

                WriteAndInstall(databasePath, builder, language, latest, ArchiveUrl, releaseIndex);

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

        /// <summary>
        /// Builds the index from an export already unpacked on disk, and puts it
        /// where the application reads it.
        ///
        /// The download is the ordinary way in. This is for working against a
        /// copy of the export by hand - trying a change to the parsing rules
        /// without fetching a gigabyte for each attempt.
        /// </summary>
        /// <remarks>
        /// The result carries no revision: a folder cannot say which commit it
        /// was taken at, so an index built this way is one the application will
        /// always offer to update.
        /// </remarks>
        public ReferenceUpdateResult BuildFromFolder(
            string databasePath,
            string language,
            string exportRoot,
            IProgress<ReferenceUpdateProgress> progress)
        {
            try
            {
                var builder = ReadExportFolder(exportRoot, language, progress);

                if (builder.Lines.Count == 0)
                {
                    return new ReferenceUpdateResult(ReferenceUpdateOutcome.Failed,
                        "The export yielded nothing; check the folder holds an 'exd' directory.", 0);
                }

                progress?.Report(new ReferenceUpdateProgress(ReferenceUpdateStage.Writing, 0, 0, 0));
                WriteAndInstall(databasePath, builder, language, string.Empty, exportRoot, null);

                return new ReferenceUpdateResult(ReferenceUpdateOutcome.Updated, string.Empty, builder.Lines.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", Convert.ToString(ex));
                return new ReferenceUpdateResult(ReferenceUpdateOutcome.Failed, ex.Message, 0);
            }
        }

        /// <summary>
        /// Reads every sheet of an unpacked export, pairing each translated file
        /// with the English one beside it.
        /// </summary>
        public static ReferenceIndexBuilder ReadExportFolder(
            string exportRoot,
            string language,
            IProgress<ReferenceUpdateProgress> progress)
        {
            var builder = new ReferenceIndexBuilder();

            foreach (var translated in Directory.EnumerateFiles(Path.Combine(exportRoot, "exd"),
                         language + ".xlf", SearchOption.AllDirectories))
            {
                var folder = Path.GetDirectoryName(translated);
                var english = Path.Combine(folder, "en.xlf");

                // A sheet nobody has started on has no English beside it, and
                // the row ids alone say nothing.
                if (!File.Exists(english))
                {
                    continue;
                }

                builder.AddSheet(
                    folder.Replace('\\', '/'),
                    File.ReadAllText(english),
                    File.ReadAllText(translated));

                if (builder.Sheets % 25 == 0)
                {
                    progress?.Report(new ReferenceUpdateProgress(
                        ReferenceUpdateStage.Downloading, 0, builder.Sheets, builder.Lines.Count));
                }
            }

            return builder;
        }

        private async Task<ReferenceIndexBuilder> DownloadAndBuildAsync(
            string language,
            IProgress<ReferenceUpdateProgress> progress,
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
            await using var counting = new CountingStream(network);
            await using var decompressed = new GZipStream(counting, CompressionMode.Decompress);
            await using var archive = new TarReader(decompressed);

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
                    }
                    else
                    {
                        translatedByFolder[folder] = content;
                    }
                }

                if (builder.Sheets % 25 == 0 && builder.Sheets > 0)
                {
                    progress?.Report(new ReferenceUpdateProgress(
                        ReferenceUpdateStage.Downloading, counting.Position, builder.Sheets, builder.Lines.Count));
                }
            }

            _logger?.LogInformation("Reference index rebuilt: {Sheets} sheets, {Lines} lines.",
                builder.Sheets, builder.Lines.Count);

            return builder;
        }

        /// <summary>
        /// The whole tail of an update: write the index beside the one in use,
        /// let go of that one, and swap them.
        ///
        /// Kept together, and reachable from a test, because everything that can
        /// go wrong here goes wrong after the download has already been paid
        /// for. The first version of this lost a finished eighty-megabyte index
        /// to a file handle it had left open itself.
        /// </summary>
        internal static void WriteAndInstall(
            string databasePath,
            ReferenceIndexBuilder builder,
            string language,
            string revision,
            string source,
            Action releaseIndex)
        {
            var directory = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            // Written beside the index and moved into place, so a download that
            // fails partway leaves the working index untouched.
            var temporaryPath = databasePath + ".new";

            try
            {
                Write(temporaryPath, builder, language, revision, source);

                releaseIndex?.Invoke();
                Install(temporaryPath, databasePath);
            }
            catch
            {
                // Eighty megabytes of a half-finished index are no use to
                // anybody, and leaving them there makes the next attempt start
                // by failing to delete them.
                Discard(temporaryPath);
                throw;
            }
        }

        private static void Discard(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        /// <summary>
        /// Puts the finished index where the application reads it.
        ///
        /// Retried, because the file has just been closed and something else on
        /// the machine may still be looking at eighty megabytes that appeared a
        /// moment ago - a virus scanner, an indexer. Losing a download that took
        /// several minutes to a handle that would have gone away by itself is
        /// not a trade worth making.
        /// </summary>
        private static void Install(string temporaryPath, string databasePath)
        {
            for (var attempt = 1; ; attempt++)
            {
                try
                {
                    File.Move(temporaryPath, databasePath, true);
                    return;
                }
                catch (IOException) when (attempt < 10)
                {
                    Thread.Sleep(500);
                }
                catch (UnauthorizedAccessException) when (attempt < 10)
                {
                    Thread.Sleep(500);
                }
            }
        }

        private static void Write(string path, ReferenceIndexBuilder builder, string language, string revision,
            string origin)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,

                // The finished file is moved into place the moment this closes,
                // and a pooled connection goes on holding it open after it is
                // closed. Windows then refuses to move it - not the file being
                // replaced, the one just written - and a download of several
                // minutes is thrown away one step from the end.
                Pooling = false
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
                // Where it came from, which is not always the project: an
                // index built from a folder says which folder.
                ["source"] = origin ?? string.Empty,
                ["revision"] = revision ?? string.Empty,

                // Which of two indexes is the newer one. The revision cannot
                // answer that - two commit shas have no order between them -
                // and one of the two may have been built from a folder and
                // carry no revision at all.
                ["built"] = DateTime.UtcNow.ToString("O", CultureInfo.InvariantCulture),
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

        /// <summary>
        /// Counts how much has come down, since the archive has no declared
        /// length and there is no percentage to show. Only counts: what is
        /// reported, and when, is decided where the sheets are read, so that
        /// one line can say both at once.
        /// </summary>
        private sealed class CountingStream : Stream
        {
            private readonly Stream _inner;
            private long _total;

            public CountingStream(Stream inner)
            {
                _inner = inner;
            }

            public override int Read(byte[] buffer, int offset, int count)
            {
                var read = _inner.Read(buffer, offset, count);
                _total += read;
                return read;
            }

            public override async ValueTask<int> ReadAsync(
                Memory<byte> buffer, CancellationToken cancellationToken = default)
            {
                var read = await _inner.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                _total += read;
                return read;
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
