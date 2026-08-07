using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace Translation.Reference
{
    /// <summary>
    /// Turns the xivrus export into something a line read off the screen can be
    /// found in.
    ///
    /// The export is XLIFF, one file per language per sheet, and the shape it
    /// arrives in is not the shape dialogue reaches us in. Every rule here was
    /// arrived at by comparing a line that came out machine-translated against
    /// what the export held for it:
    ///
    /// - A translated file's &lt;source&gt; holds the row id, not the text, so
    ///   the English comes from the file beside it, joined on that id.
    /// - Quest text is stored as KEY&lt;tab&gt;TEXT, and only the first tab
    ///   separates; the rest are tabs in the line.
    /// - Some markup only styles what is already there and the words survive on
    ///   screen; some is drawn as the literal text it looks like; and some the
    ///   game substitutes as it draws. Only the last makes a line unusable.
    /// - Battle and cutscene lines carry their speaker inside the text.
    /// </summary>
    public sealed class ReferenceIndexBuilder
    {
        /// <summary>
        /// One row: its number, and what the translators put against it.
        ///
        /// The source is matched as anything but a tag, and that is what keeps
        /// this inside the row it started in. A row nobody has translated is
        /// written &lt;target/&gt;, which has nothing to match, and a dot free
        /// to cross the row's end would run on to the next row's target and
        /// file its text under this row's number. That is not a line lost, it
        /// is a line answered with somebody else's - 1 281 of them across the
        /// export, each shown as a translation made by hand.
        /// </summary>
        private static readonly Regex Unit = new Regex(
            "<trans-unit id=\"([^\"]+)\"[^>]*>\\s*<source>[^<]*</source>\\s*" +
            "<target(?: state=\"([^\"]*)\")?>(.*?)</target>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Styling: emphasis, the pair around a highlighted term, page and word
        /// breaks, colour. The words around it are drawn as written, so taking
        /// the tags out leaves a line that matches - discarding the line instead
        /// costs 140,000 of them.
        /// </summary>
        private static readonly Regex Formatting = new Regex(
            "<var (?:1A|48|49|17|1F|1B|1C|60)[^>]*>|</?(?:color2|glow2|color|glow)[^>]*>",
            RegexOptions.Compiled);

        private static readonly Regex LineBreak = new Regex("<nl>", RegexOptions.Compiled);

        /// <summary>
        /// A place the game may break a long word, drawn as nothing until it
        /// does. German is full of them - "Waffenfertigkeiten" is stored as
        /// "Waf&lt;var 16 /var&gt;fen&lt;var 16 /var&gt;fer&lt;var 16 /var&gt;tig..." - and
        /// counting them as substitutions threw the line away.
        /// </summary>
        private static readonly Regex SoftHyphen = new Regex("<var 16[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// A space the game will not break a line at: between a number and its
        /// unit, or before the ellipsis that German and French set off with
        /// one. Drawn as a space, so it is one.
        /// </summary>
        private static readonly Regex HardSpace = new Regex("<var 1D[^>]*>", RegexOptions.Compiled);

        /// <summary>
        /// Anything the game substitutes as it draws. Recognised by being a
        /// &lt;var&gt;: sound cues like &lt;sigh&gt; are drawn as the text they
        /// look like and belong in the line.
        /// </summary>
        private static readonly Regex Dynamic = new Regex("<var [^>]*>", RegexOptions.Compiled);

        /// <summary>The player's own name, written in as the game draws.</summary>
        private static readonly Regex Player = new Regex(
            "<var 2C .*?\\)\\) [0-9A-F]{2} /var>", RegexOptions.Singleline | RegexOptions.Compiled);

        /// <summary>
        /// Agreement with the player's gender, feminine first. E905 alone: the
        /// other condition codes ask about things that cannot be answered here,
        /// and one of them distinguishes a joystick from a mini-joystick.
        /// </summary>
        private static readonly Regex GenderAgreement = new Regex(
            "<var 08 E905 \\(\\((.*?)\\)\\) \\(\\((.*?)\\)\\) /var>",
            RegexOptions.Singleline | RegexOptions.Compiled);

        private static readonly Regex SpeakerPrefix = new Regex(
            "^\\(-([^)]{0,60})-\\)", RegexOptions.Compiled);

        private const string KeySeparator = "<tab>";

        /// <summary>Stands in for the name; a control character cannot collide.</summary>
        public const string PlayerPlaceholder = "\u0001";

        /// <summary>The game's own list of who everyone is, as "Name&lt;tab&gt;Title".</summary>
        private const string NpcSheet = "ENpcResident";

        private readonly Dictionary<string, string> _lines = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _patterns = new Dictionary<string, string>(StringComparer.Ordinal);
        private readonly Dictionary<string, string> _speakers = new Dictionary<string, string>(StringComparer.Ordinal);

        private readonly Dictionary<(string Source, bool Feminine), string> _gendered =
            new Dictionary<(string, bool), string>();

        public IReadOnlyDictionary<string, string> Lines => _lines;

        public IReadOnlyDictionary<string, string> Patterns => _patterns;

        public IReadOnlyDictionary<string, string> Speakers => _speakers;

        public IReadOnlyDictionary<(string Source, bool Feminine), string> Gendered => _gendered;

        public int SkippedForMarkup { get; private set; }

        /// <summary>Sheets that had an English side to pair with, so were read.</summary>
        public int Sheets { get; private set; }

        public int Conflicts { get; private set; }

        /// <summary>
        /// Takes one sheet: the English file and its translation, as they stand
        /// in the export.
        /// </summary>
        public void AddSheet(string folder, string englishXliff, string translatedXliff)
        {
            var english = ParseUnits(englishXliff);
            if (english.Count == 0)
            {
                return;
            }

            Sheets++;

            var isRoster = FolderName(folder) == NpcSheet;

            foreach (Match match in Unit.Matches(translatedXliff ?? string.Empty))
            {
                if (!english.TryGetValue(match.Groups[1].Value, out var englishText))
                {
                    continue;
                }

                var translatedText = Unescape(match.Groups[3].Value);

                if (isRoster)
                {
                    // "Name<tab>Title" - only the name is ever spoken.
                    AddSpeaker(FirstField(englishText), FirstField(translatedText));
                    continue;
                }

                var englishSpeaker = SpeakerPrefix.Match(StripKey(englishText));
                var translatedSpeaker = SpeakerPrefix.Match(StripKey(translatedText));
                if (englishSpeaker.Success && translatedSpeaker.Success)
                {
                    AddSpeaker(englishSpeaker.Groups[1].Value, translatedSpeaker.Groups[1].Value);
                }

                Add(englishText, translatedText);
            }
        }

        private void Add(string englishText, string translatedText)
        {
            var english = Normalize(englishText);
            var translated = Normalize(translatedText);

            if (english.Length == 0 || translated.Length == 0 ||
                string.Equals(english, translated, StringComparison.Ordinal))
            {
                return;
            }

            // Gender agreement with nothing else left to substitute: keep the
            // line both ways. Both sides can carry it - English says "this
            // woman" against "this man" - so the key differs by character too.
            if (GenderAgreement.IsMatch(english) || GenderAgreement.IsMatch(translated))
            {
                var feminineEnglish = GenderAgreement.Replace(english, m => m.Groups[1].Value);
                var masculineEnglish = GenderAgreement.Replace(english, m => m.Groups[2].Value);
                var feminine = GenderAgreement.Replace(translated, m => m.Groups[1].Value);
                var masculine = GenderAgreement.Replace(translated, m => m.Groups[2].Value);

                if (!Dynamic.IsMatch(feminineEnglish) && !Dynamic.IsMatch(masculineEnglish) &&
                    !Dynamic.IsMatch(feminine) && !Dynamic.IsMatch(masculine))
                {
                    _gendered[(feminineEnglish, true)] = feminine;
                    _gendered[(masculineEnglish, false)] = masculine;
                    return;
                }
            }

            // At most one placeholder a side, and at least one somewhere:
            // usable as a pattern. More than one and the pieces between them
            // stop pinning the line down.
            //
            // The two sides need not agree about naming the character, and
            // requiring that they did cost whole conversations. Languages
            // address the player in different places: German says "Und er wird
            // deine Hilfe benötigen", where the Russian for the same row says
            // "Поторопись, <name>". Whichever side carries the name, the other
            // is a fixed string - so the line can still be matched, and the
            // name written into whichever side has room for it.
            var englishPlayer = Player.Replace(english, PlayerPlaceholder);
            var translatedPlayer = Player.Replace(translated, PlayerPlaceholder);
            var sourceNames = Count(englishPlayer, PlayerPlaceholder);
            var translatedNames = Count(translatedPlayer, PlayerPlaceholder);
            var isPattern = sourceNames <= 1 && translatedNames <= 1 && sourceNames + translatedNames > 0;

            var target = _lines;
            if (isPattern)
            {
                english = englishPlayer;
                translated = translatedPlayer;
                target = _patterns;
            }

            if (Dynamic.IsMatch(english) || Dynamic.IsMatch(translated))
            {
                SkippedForMarkup++;
                return;
            }

            if (!target.TryGetValue(english, out var existing))
            {
                target[english] = translated;
            }
            else if (!string.Equals(existing, translated, StringComparison.Ordinal))
            {
                // The same line said in two places, rendered differently.
                // Keeping the first is arbitrary but stable.
                Conflicts++;
            }
        }

        private void AddSpeaker(string englishName, string translatedName)
        {
            var english = FoldApostrophes(Collapse(englishName));
            var translated = Collapse(translatedName);

            if (english.Length == 0 || translated.Length == 0 ||
                string.Equals(english, translated, StringComparison.Ordinal))
            {
                return;
            }

            if (Dynamic.IsMatch(english) || Dynamic.IsMatch(translated))
            {
                return;
            }

            // "???" is the game keeping an identity back, and the Russian
            // wrapper gives it away. A label with no letters in it is a
            // placeholder, and translating it spoils the scene it was hiding.
            if (!english.Any(char.IsLetter))
            {
                return;
            }

            _speakers[english] = translated;
        }

        /// <summary>
        /// Reduces a stored line to the form the reader hands us: the game wraps
        /// dialogue where it likes and we read it joined.
        /// </summary>
        internal static string Normalize(string text)
        {
            text = StripKey(text);
            text = Formatting.Replace(text, string.Empty);
            text = SoftHyphen.Replace(text, string.Empty);
            text = HardSpace.Replace(text, " ");
            text = LineBreak.Replace(text, " ");
            text = SpeakerPrefix.Replace(text.TrimStart(), string.Empty);
            return Collapse(text.Replace(KeySeparator, " "));
        }

        internal static string FoldApostrophes(string text)
        {
            return text.Replace('’', '\'').Replace('ʼ', '\'').Replace('‘', '\'');
        }

        private static string StripKey(string text)
        {
            var separator = text.IndexOf(KeySeparator, StringComparison.Ordinal);
            return separator >= 0 ? text.Substring(separator + KeySeparator.Length) : text;
        }

        private static string FirstField(string text)
        {
            var separator = text.IndexOf(KeySeparator, StringComparison.Ordinal);
            return separator >= 0 ? text.Substring(0, separator) : text;
        }

        private static string Collapse(string text)
        {
            var builder = new StringBuilder(text.Length);
            var pendingSpace = false;

            foreach (var character in text)
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

        private static int Count(string text, string needle)
        {
            var found = 0;
            var at = text.IndexOf(needle, StringComparison.Ordinal);
            while (at >= 0)
            {
                found++;
                at = text.IndexOf(needle, at + needle.Length, StringComparison.Ordinal);
            }

            return found;
        }

        private static string FolderName(string folder)
        {
            if (string.IsNullOrEmpty(folder))
            {
                return string.Empty;
            }

            var separator = folder.LastIndexOf('/');
            return separator >= 0 ? folder.Substring(separator + 1) : folder;
        }

        private static Dictionary<string, string> ParseUnits(string xliff)
        {
            var units = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (Match match in Unit.Matches(xliff ?? string.Empty))
            {
                units[match.Groups[1].Value] = Unescape(match.Groups[3].Value);
            }

            return units;
        }

        private static string Unescape(string text)
        {
            return text
                .Replace("&lt;", "<")
                .Replace("&gt;", ">")
                .Replace("&quot;", "\"")
                .Replace("&apos;", "'")
                .Replace("&amp;", "&");
        }
    }
}
