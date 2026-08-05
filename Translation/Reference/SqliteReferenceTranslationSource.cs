using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Data.Sqlite;

using Microsoft.Extensions.Logging;

namespace Translation.Reference
{
    /// <summary>
    /// Looks lines up in the index built by scripts/build_reference_translations.py.
    ///
    /// Held open read-only for the life of the app: the file is around sixty
    /// megabytes and every NPC line asks it a question, so opening per lookup
    /// would cost more than the lookup.
    /// </summary>
    public sealed class SqliteReferenceTranslationSource : IReferenceTranslationSource, IDisposable
    {
        private readonly ILogger _logger;
        private readonly object _sync = new object();

        /// <summary>Stands in for the character's name inside a stored pattern.</summary>
        private const string PlayerPlaceholder = "\u0001";

        private string _playerName = string.Empty;

        private Dictionary<string, string> _addressedToPlayer;

        private SqliteConnection _connection;
        private SqliteCommand _lookup;
        private SqliteParameter _sentenceParameter;

        public SqliteReferenceTranslationSource(string databasePath, ILogger logger)
        {
            _logger = logger;
            LanguageCode = string.Empty;

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                return;
            }

            var resolvedPath = Path.IsPathRooted(databasePath)
                ? databasePath
                : Path.Combine(AppContext.BaseDirectory, databasePath);

            if (!File.Exists(resolvedPath))
            {
                _logger?.LogInformation(
                    "Reference translations are not installed at {Path}; every line will be translated.",
                    resolvedPath);
                return;
            }

            try
            {
                Open(resolvedPath);
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", Convert.ToString(ex));
                Dispose();
            }
        }

        public string LanguageCode { get; private set; }

        public bool IsAvailable => _lookup != null;

        /// <summary>
        /// Setting this fills the name into every stored pattern at once.
        ///
        /// Matching them one at a time would mean testing a line against three
        /// thousand patterns; the name does not change while the game runs, so
        /// filling it in turns them into ordinary lines that are found the same
        /// way as the rest.
        /// </summary>
        public string PlayerName
        {
            get => _playerName;
            set
            {
                var name = (value ?? string.Empty).Trim();
                lock (_sync)
                {
                    if (string.Equals(_playerName, name, StringComparison.Ordinal))
                    {
                        return;
                    }

                    _playerName = name;
                    _addressedToPlayer = name.Length > 0 ? BuildAddressedLines(name) : null;
                }
            }
        }

        public bool TryGetTranslation(string sentence, out string translation)
        {
            translation = string.Empty;

            var key = Normalize(sentence);
            if (key.Length == 0 || _lookup == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (_lookup == null)
                {
                    return false;
                }

                if (_addressedToPlayer != null &&
                    _addressedToPlayer.TryGetValue(key, out var addressed) &&
                    !CarriesUnresolvedMarkup(addressed))
                {
                    translation = addressed;
                    return true;
                }

                try
                {
                    _sentenceParameter.Value = key;
                    var found = _lookup.ExecuteScalar() as string;
                    if (string.IsNullOrEmpty(found) || CarriesUnresolvedMarkup(found))
                    {
                        return false;
                    }

                    translation = found;
                    return true;
                }
                catch (Exception ex)
                {
                    _logger?.LogInformation("{Message}", Convert.ToString(ex));
                    return false;
                }
            }
        }

        /// <summary>
        /// Whether a stored line still carries markup the game would have
        /// resolved as it drew - gender agreement, the player's name.
        ///
        /// The builder leaves these out, and a stricter builder is the real
        /// answer. This is the net under it: one such line reached the chat
        /// window reading "когда ты с ним &lt;var 08 E905 ((схлестнулась))
        /// ((схлестнулся)) /var&gt;", and showing that is worse than paying a
        /// translator for the line.
        ///
        /// Only &lt;var&gt; counts. Sound cues - &lt;sigh&gt;, &lt;click&gt;,
        /// &lt;gasp&gt; - look like markup and are not: the game draws them as
        /// the text they appear to be, so they belong in the line.
        /// </summary>
        private static bool CarriesUnresolvedMarkup(string translation)
        {
            var opening = translation.IndexOf("<var ", StringComparison.Ordinal);
            return opening >= 0 && translation.IndexOf('>', opening) > opening;
        }

        /// <summary>
        /// Reduces a line to the shape the index was built in: the game wraps
        /// dialogue across lines and we read it back joined, so runs of
        /// whitespace cannot be part of the key.
        /// </summary>
        internal static string Normalize(string sentence)
        {
            if (string.IsNullOrWhiteSpace(sentence))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(sentence.Length);
            var pendingSpace = false;

            foreach (var character in sentence)
            {
                if (char.IsWhiteSpace(character))
                {
                    pendingSpace = builder.Length > 0;
                    continue;
                }

                if (pendingSpace)
                {
                    builder.Append(' ');
                    pendingSpace = false;
                }

                builder.Append(character);
            }

            return builder.ToString();
        }

        /// <summary>
        /// The ways the game may write a character called "D'ark One": in full,
        /// or by either half of the name.
        ///
        /// Which one appears is the line's own choice, and guessing wrong costs
        /// the match: every line addressed to the player went to a translator
        /// while only the full name was tried, because what the game had
        /// written was "Go swiftly, D'ark."
        /// </summary>
        private static IEnumerable<string> NameForms(string playerName)
        {
            yield return playerName;

            var separator = playerName.IndexOf(' ');
            if (separator <= 0)
            {
                yield break;
            }

            yield return playerName.Substring(0, separator);

            var surname = playerName.Substring(separator + 1).Trim();
            if (surname.Length > 0)
            {
                yield return surname;
            }
        }

        /// <summary>
        /// Reads the patterns and writes the name into each, giving the lines
        /// as this particular character hears them.
        /// </summary>
        private Dictionary<string, string> BuildAddressedLines(string playerName)
        {
            var lines = new Dictionary<string, string>(StringComparer.Ordinal);
            if (_connection == null)
            {
                return lines;
            }

            try
            {
                using (var command = _connection.CreateCommand())
                {
                    command.CommandText = "SELECT source, translated FROM pattern";
                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var sourcePattern = reader.GetString(0);
                            var translatedPattern = reader.GetString(1);

                            foreach (var form in NameForms(playerName))
                            {
                                var source = Normalize(sourcePattern.Replace(PlayerPlaceholder, form));
                                if (source.Length > 0)
                                {
                                    // The line is stored under each form, but the
                                    // translation reads the way the line did.
                                    lines[source] = translatedPattern.Replace(PlayerPlaceholder, form);
                                }
                            }
                        }
                    }
                }

                _logger?.LogInformation("Lines addressed to {Player}: {Count}.", playerName, lines.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", Convert.ToString(ex));
            }

            return lines;
        }

        private void Open(string resolvedPath)
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = resolvedPath,
                Mode = SqliteOpenMode.ReadOnly,
                Cache = SqliteCacheMode.Shared
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            using (var meta = _connection.CreateCommand())
            {
                meta.CommandText = "SELECT value FROM meta WHERE key = 'language'";
                LanguageCode = meta.ExecuteScalar() as string ?? string.Empty;
            }

            _lookup = _connection.CreateCommand();
            _lookup.CommandText = "SELECT translated FROM line WHERE source = $sentence";
            _sentenceParameter = _lookup.CreateParameter();
            _sentenceParameter.ParameterName = "$sentence";
            _lookup.Parameters.Add(_sentenceParameter);
            _lookup.Prepare();

            using (var count = _connection.CreateCommand())
            {
                count.CommandText = "SELECT value FROM meta WHERE key = 'lines'";
                _logger?.LogInformation("Reference translations loaded: {Lines} lines of {Language}.",
                    count.ExecuteScalar() as string ?? "?", LanguageCode);
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _lookup?.Dispose();
                _lookup = null;
                _connection?.Dispose();
                _connection = null;
            }
        }
    }
}
