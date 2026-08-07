using System;
using System.IO;

using FFXIVTataruHelper.Services.Logging;

namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// The language the game is being played in, read from the game.
    ///
    /// Not from anything chosen in this application. What a line says when it
    /// is read off the screen is decided by the client, and the translation
    /// index is keyed on that language - so asking the settings window, or
    /// guessing from the words of one line, answers a different question. A
    /// German player writing in chat would otherwise have moved the whole
    /// index out from under the next line of dialogue.
    /// </summary>
    public static class GameClientLanguage
    {
        private const string ConfigFolder = @"My Games\FINAL FANTASY XIV - A Realm Reborn";
        private const string ConfigFile = "FFXIV.cfg";

        /// <summary>
        /// The language code the game is set to, or empty when it cannot be
        /// read - the game may never have been run on this machine, or keep its
        /// settings somewhere this does not know about.
        /// </summary>
        public static string Detect(IAppLogger logger)
        {
            try
            {
                var path = ConfigPath();
                if (path.Length == 0 || !File.Exists(path))
                {
                    logger?.WriteLog("The game's configuration was not found at '" + path +
                                     "'; its language is unknown.");
                    return string.Empty;
                }

                var language = Parse(File.ReadAllText(path));
                logger?.WriteLog(language.Length > 0
                    ? "The game is set to '" + language + "', by " + path + "."
                    : "The game's configuration at " + path + " does not name a language.");

                return language;
            }
            catch (Exception ex)
            {
                logger?.WriteLog("Failed to read the game's language.");
                logger?.WriteLog(ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// The same language as the memory reader names it. Anything unknown is
        /// English, which is what it assumed before it asked at all.
        /// </summary>
        public static string ReaderName(string code)
        {
            switch (code)
            {
                case "ja": return "Japanese";
                case "de": return "German";
                case "fr": return "French";
                default: return "English";
            }
        }

        internal static string ConfigPath()
        {
            var documents = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            return documents.Length == 0 ? string.Empty : Path.Combine(documents, ConfigFolder, ConfigFile);
        }

        /// <summary>
        /// Picks the language out of the game's configuration file, which is a
        /// list of "name<![CDATA[\t]]>value" lines. The value is the game's own
        /// numbering, and the four it has are the four the game is published in.
        /// </summary>
        internal static string Parse(string configuration)
        {
            foreach (var line in (configuration ?? string.Empty).Split('\n'))
            {
                var trimmed = line.Trim();
                if (!trimmed.StartsWith("Language", StringComparison.Ordinal))
                {
                    continue;
                }

                // "Language" is a prefix of nothing else in the file today, but
                // a "LanguageSomething" appearing later should not be read as
                // this one.
                var value = trimmed.Substring("Language".Length).Trim();
                if (value.Length == 0 || !int.TryParse(value, out var code))
                {
                    continue;
                }

                switch (code)
                {
                    case 0: return "ja";
                    case 1: return "en";
                    case 2: return "de";
                    case 3: return "fr";
                    default: return string.Empty;
                }
            }

            return string.Empty;
        }
    }
}
