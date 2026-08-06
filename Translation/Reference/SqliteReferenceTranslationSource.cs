using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using Microsoft.Data.Sqlite;

using Microsoft.Extensions.Logging;

namespace Translation.Reference
{
    /// <summary>
    /// Looks lines up in the index built by <see cref="ReferenceIndexUpdater"/>.
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

        private bool? _playerIsFeminine;

        private Dictionary<string, string> _genderedLines;

        private SqliteCommand _speakerLookup;

        private SqliteParameter _speakerParameter;

        private SqliteConnection _connection;
        private SqliteCommand _lookup;
        private SqliteParameter _sentenceParameter;

        public SqliteReferenceTranslationSource(string databasePath, ILogger logger)
        {
            _logger = logger;
            LanguageCode = string.Empty;
            Revision = string.Empty;

            if (string.IsNullOrWhiteSpace(databasePath))
            {
                return;
            }

            var resolvedPath = Resolve(databasePath);
            DatabasePath = resolvedPath;

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

        public string Revision { get; private set; }

        public int LineCount { get; private set; }

        /// <summary>
        /// Where the index was looked for, whether or not one was found. The
        /// path is settled here rather than by the caller, and rebuilding the
        /// index has to write to the same file this reads.
        /// </summary>
        public string DatabasePath { get; private set; } = string.Empty;

        public bool IsAvailable => _lookup != null;

        /// <summary>
        /// A path from settings, made absolute.
        ///
        /// Environment variables are expanded first, so the settings can name
        /// the user's own folder - which is where the index lives - without
        /// this project having to know what that folder is called.
        /// </summary>
        public static string Resolve(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                return string.Empty;
            }

            var expanded = Environment.ExpandEnvironmentVariables(databasePath);

            return Path.IsPathRooted(expanded)
                ? Path.GetFullPath(expanded)
                : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, expanded));
        }

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

        /// <summary>
        /// Setting this picks the wording the Russian agrees with, for the five
        /// thousand lines that have one. English usually needs no such choice -
        /// "adventurer" has no gender - so these are lines the index would
        /// otherwise have had to leave to an engine.
        /// </summary>
        public bool? PlayerIsFeminine
        {
            get => _playerIsFeminine;
            set
            {
                lock (_sync)
                {
                    if (_playerIsFeminine == value)
                    {
                        return;
                    }

                    _playerIsFeminine = value;
                    _genderedLines = value.HasValue ? BuildGenderedLines(value.Value) : null;
                }
            }
        }

        public bool TryGetSpeakerName(string speaker, out string translated)
        {
            translated = string.Empty;

            var key = FoldApostrophes(Normalize(speaker));
            if (key.Length == 0 || _connection == null)
            {
                return false;
            }

            lock (_sync)
            {
                if (_speakerLookup == null)
                {
                    return false;
                }

                try
                {
                    _speakerParameter.Value = key;
                    var found = _speakerLookup.ExecuteScalar() as string;
                    if (string.IsNullOrEmpty(found))
                    {
                        return false;
                    }

                    translated = found;
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
        /// The game writes a typographic apostrophe in names - Y’shtola - and a
        /// plain one elsewhere for the same character, so the two have to look
        /// alike before anything is compared.
        /// </summary>
        internal static string FoldApostrophes(string text)
        {
            return text
                .Replace('’', '\'')
                .Replace('ʼ', '\'')
                .Replace('‘', '\'');
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

                if (_genderedLines != null &&
                    _genderedLines.TryGetValue(key, out var gendered) &&
                    !CarriesUnresolvedMarkup(gendered))
                {
                    translation = gendered;
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
        /// Reads the lines the Russian phrases differently for a man and a
        /// woman, keeping the wording that fits this character.
        /// </summary>
        private Dictionary<string, string> BuildGenderedLines(bool isFeminine)
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
                    // Both the line and its translation are kept per gender:
                    // English says "this woman" against "this man" as readily
                    // as Russian declines around it, so even the key differs.
                    command.CommandText = "SELECT source, translated FROM gendered WHERE feminine = $feminine";
                    var feminine = command.CreateParameter();
                    feminine.ParameterName = "$feminine";
                    feminine.Value = isFeminine ? 1 : 0;
                    command.Parameters.Add(feminine);

                    using (var reader = command.ExecuteReader())
                    {
                        while (reader.Read())
                        {
                            var source = Normalize(reader.GetString(0));
                            if (source.Length > 0)
                            {
                                lines[source] = reader.GetString(1);
                            }
                        }
                    }
                }

                _logger?.LogInformation("Lines phrased for a {Gender} character: {Count}.",
                    isFeminine ? "female" : "male", lines.Count);
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", Convert.ToString(ex));
            }

            return lines;
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
                Cache = SqliteCacheMode.Shared,

                // A pooled connection keeps the file open after it is closed,
                // and rebuilding the index has to move a new file over this
                // one. Nothing is gained by pooling here anyway: this is one
                // connection held open for as long as the application runs.
                Pooling = false
            }.ToString();

            _connection = new SqliteConnection(connectionString);
            _connection.Open();

            LanguageCode = ReadMeta("language");

            // Written only by an index this application built. One built from a
            // folder cannot say which commit it holds, and then every update
            // downloads rather than trusting a revision nobody recorded.
            Revision = ReadMeta("revision");
            LineCount = int.TryParse(ReadMeta("lines"), out var lines) ? lines : 0;

            _lookup = _connection.CreateCommand();
            _lookup.CommandText = "SELECT translated FROM line WHERE source = $sentence";
            _sentenceParameter = _lookup.CreateParameter();
            _sentenceParameter.ParameterName = "$sentence";
            _lookup.Parameters.Add(_sentenceParameter);
            _lookup.Prepare();


            // An index built before names were collected has no such table, and
            // that has to cost only the names rather than the whole index.
            try
            {
                _speakerLookup = _connection.CreateCommand();
                _speakerLookup.CommandText = "SELECT translated FROM speaker WHERE source = $speaker";
                _speakerParameter = _speakerLookup.CreateParameter();
                _speakerParameter.ParameterName = "$speaker";
                _speakerLookup.Parameters.Add(_speakerParameter);
                _speakerLookup.Prepare();
            }
            catch (SqliteException)
            {
                _speakerLookup?.Dispose();
                _speakerLookup = null;
                _logger?.LogInformation("This index carries no speaker names.");
            }

            // The path is part of the message on purpose: which file answered a
            // line is otherwise a guess, and guessing has been expensive here.
            _logger?.LogInformation("Reference translations loaded: {Lines} lines of {Language} from {Path}.",
                LineCount, LanguageCode, resolvedPath);
        }

        private string ReadMeta(string key)
        {
            try
            {
                using var meta = _connection.CreateCommand();
                meta.CommandText = "SELECT value FROM meta WHERE key = $key";
                var parameter = meta.CreateParameter();
                parameter.ParameterName = "$key";
                parameter.Value = key;
                meta.Parameters.Add(parameter);
                return meta.ExecuteScalar() as string ?? string.Empty;
            }
            catch (SqliteException)
            {
                return string.Empty;
            }
        }

        public void Dispose()
        {
            lock (_sync)
            {
                _speakerLookup?.Dispose();
                _speakerLookup = null;
                _lookup?.Dispose();
                _lookup = null;
                _connection?.Dispose();
                _connection = null;
            }
        }
    }
}
