using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace FFXIVTataruHelper.Services.Diagnostics
{
    /// <summary>
    /// Writes out what a session looks like from the inside, for pasting into a
    /// bug report.
    ///
    /// Deliberately more than a dump of values. Every report of "it does not
    /// translate" so far has come down to one of a handful of causes that the
    /// person reporting it cannot see and we cannot guess: the switch is off,
    /// the game is not attached, its memory is not readable on that client, one
    /// of the two dialogue channels is readable and the other is not, or the
    /// channel is simply unticked in the window. So the report names the ones it
    /// can see, in plain words, before listing the numbers behind them.
    /// </summary>
    public static class DiagnosticsReport
    {
        private const string DialogueCode = "003D";
        private const string CutsceneCode = "0044";

        public static string Build(DiagnosticsSnapshot snapshot, DateTime timestamp)
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            var report = new StringBuilder();

            report.Append("Tataru Helper diagnostics - ")
                .AppendLine(timestamp.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));

            report.AppendLine();
            report.AppendLine("What this looks like");
            foreach (var finding in Describe(snapshot))
            {
                report.Append("  - ").AppendLine(finding);
            }

            report.AppendLine();
            report.AppendLine("Application");
            Field(report, "Version", snapshot.AppVersion + (snapshot.IsInstalled ? " (installed)" : " (portable)"));
            Field(report, "Windows", snapshot.OperatingSystem);
            Field(report, "Administrator", YesNo(snapshot.IsElevated));
            Field(report, "Interface", snapshot.UiLanguage);

            var reading = snapshot.Reading;

            report.AppendLine();
            report.AppendLine("Game");
            Field(report, "Process", reading.GameAttached ? reading.ProcessDescription : "not attached");
            Field(report, "Language", reading.GameLanguage);
            Field(report, "Character read", YesNo(reading.PlayerResolved));
            Field(report, "Real-Time Translation", OnOff(reading.RealtimeEnabled));
            Field(report, "Lines read live", reading.LinesReadLive.ToString(CultureInfo.InvariantCulture));
            Field(report, "Codes read live", reading.CodesReadLive.Count == 0
                ? "none"
                : string.Join(", ", reading.CodesReadLive));

            report.AppendLine();
            report.AppendLine("XIV Rus Translation");
            Field(report, "Enabled", YesNo(snapshot.ReferenceTranslationEnabled));
            Field(report, "Index", snapshot.ReferenceIndex);

            report.AppendLine();
            report.AppendLine("Chat windows");
            if (snapshot.Windows.Count == 0)
            {
                report.AppendLine("  none");
            }

            foreach (var window in snapshot.Windows)
            {
                report.Append("  ").Append(window.Name).Append(": ").Append(window.Engine)
                    .Append(", ").Append(window.FromLanguage).Append(" -> ").AppendLine(window.ToLanguage);
                report.Append("    NPC dialogue ").Append(YesNo(IsTicked(window, DialogueCode)))
                    .Append(", cutscene dialogue ").AppendLine(YesNo(IsTicked(window, CutsceneCode)));
                report.Append("    ticked: ").AppendLine(window.TickedCodes.Count == 0
                    ? "nothing"
                    : string.Join(", ", window.TickedCodes));
            }

            report.AppendLine();
            report.Append("Log: ").AppendLine(snapshot.LogPath);

            return report.ToString();
        }

        /// <summary>
        /// The report in plain words: what is working, and what the evidence says
        /// is not.
        /// </summary>
        internal static IReadOnlyList<string> Describe(DiagnosticsSnapshot snapshot)
        {
            var findings = new List<string>();
            var reading = snapshot.Reading;

            if (!reading.GameAttached)
            {
                findings.Add("The game is not attached, so nothing can be translated. " +
                             "Start Final Fantasy XIV, and run Tataru Helper as administrator.");
                return findings;
            }

            if (!reading.PlayerResolved)
            {
                findings.Add("The game is attached but its memory has not been read: not even the " +
                             "character could be. Either the character is not logged in yet, or this " +
                             "client is one we cannot read - a region or a patch we have not caught up with.");
            }

            if (!reading.RealtimeEnabled)
            {
                findings.Add("Real-Time Translation is switched off, so dialogue only arrives once the " +
                             "player clicks through it, and voiced cutscene subtitles never arrive at all - " +
                             "the game does not write those to the chat log.");
            }
            else
            {
                var readsDialogue = HasReadLive(reading.CodesReadLive, DialogueCode);
                var readsCutscenes = HasReadLive(reading.CodesReadLive, CutsceneCode);

                if (!readsDialogue && !readsCutscenes)
                {
                    findings.Add("No dialogue has been read off the screen yet. Either nobody has spoken " +
                                 "since Tataru Helper started, or reading the game's interface is not " +
                                 "working on this client.");
                }
                else if (readsDialogue && !readsCutscenes)
                {
                    findings.Add("NPC dialogue is being read off the screen, but no cutscene subtitle has " +
                                 "been. Either no cutscene has played yet, or subtitles are not readable on " +
                                 "this client - they are read from a different place than speech, and that " +
                                 "place moves with each game patch.");
                }
                else if (!readsDialogue && readsCutscenes)
                {
                    findings.Add("Cutscene subtitles are being read off the screen, but no NPC dialogue has " +
                                 "been. Either no NPC has spoken yet, or speech is not readable on this client.");
                }
                else
                {
                    findings.Add("Both NPC dialogue and cutscene subtitles are being read off the screen.");
                }
            }

            foreach (var window in snapshot.Windows)
            {
                var missing = new List<string>();
                if (!IsTicked(window, DialogueCode))
                {
                    missing.Add("NPC dialogue");
                }

                if (!IsTicked(window, CutsceneCode))
                {
                    missing.Add("cutscene dialogue");
                }

                if (missing.Count > 0)
                {
                    findings.Add($"Window {window.Name} is not listening to " + string.Join(" or ", missing) +
                                 " - those are unticked in its chat codes, so it will not show them however " +
                                 "they arrive.");
                }
            }

            return findings;
        }

        private static bool HasReadLive(IReadOnlyList<string> codes, string code)
        {
            return codes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase));
        }

        private static bool IsTicked(DiagnosticsWindow window, string code)
        {
            return window.TickedCodes.Any(x => string.Equals(x, code, StringComparison.OrdinalIgnoreCase));
        }

        private static void Field(StringBuilder report, string name, string value)
        {
            report.Append("  ").Append(name.PadRight(22)).AppendLine(
                string.IsNullOrWhiteSpace(value) ? "unknown" : value);
        }

        private static string YesNo(bool value) => value ? "yes" : "no";

        private static string OnOff(bool value) => value ? "on" : "off";
    }
}
