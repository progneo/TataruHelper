using System;
using System.Linq;

using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.Diagnostics;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.Diagnostics
{
    // The point of the report is the plain-words part: whoever reads a pasted
    // copy of it should not have to work out what the numbers mean. Each case
    // here is a way "it does not translate" has actually turned out.
    [TestFixture]
    public class DiagnosticsReportTests
    {
        [Test]
        public void SaysSoWhenTheGameWasNeverFound()
        {
            var findings = DiagnosticsReport.Describe(Snapshot(Reading(attached: false)));

            Assert.That(findings, Has.Count.EqualTo(1), "nothing else is worth saying yet");
            Assert.That(findings[0], Does.Contain("not attached"));
        }

        [Test]
        public void SaysSoWhenAttachedButUnreadable()
        {
            var findings = DiagnosticsReport.Describe(Snapshot(Reading(playerResolved: false)));

            Assert.That(findings.Any(x => x.Contains("has not been read")), Is.True);
        }

        [Test]
        public void SaysWhyCutscenesAreSilentWhenRealtimeIsOff()
        {
            var findings = DiagnosticsReport.Describe(Snapshot(Reading(realtimeEnabled: false)));

            Assert.Multiple(() =>
            {
                Assert.That(findings.Any(x => x.Contains("switched off")), Is.True);
                Assert.That(findings.Any(x => x.Contains("chat log")), Is.True,
                    "the reason cutscenes give nothing at all");
            });
        }

        // The report that started all this: speech translated, cutscenes silent.
        [Test]
        public void SeparatesAReadableChannelFromAnUnreadableOne()
        {
            var findings = DiagnosticsReport.Describe(Snapshot(Reading(codesReadLive: new[] { "003D" })));

            Assert.That(findings.Any(x =>
                    x.Contains("NPC dialogue is being read") && x.Contains("no cutscene subtitle")),
                Is.True);
        }

        [Test]
        public void SaysNothingIsBeingReadWhenNeitherChannelHasArrived()
        {
            var findings = DiagnosticsReport.Describe(Snapshot(Reading(codesReadLive: Array.Empty<string>())));

            Assert.That(findings.Any(x => x.Contains("No dialogue has been read")), Is.True);
        }

        [Test]
        public void ConfirmsWhenBothChannelsAreBeingRead()
        {
            var findings = DiagnosticsReport.Describe(
                Snapshot(Reading(codesReadLive: new[] { "003D", "0044" })));

            Assert.That(findings.Any(x => x.Contains("Both NPC dialogue and cutscene subtitles")), Is.True);
        }

        [Test]
        public void NamesAWindowThatIsNotListeningToDialogue()
        {
            var snapshot = Snapshot(Reading(codesReadLive: new[] { "003D", "0044" }));
            snapshot.Windows = new[]
            {
                new DiagnosticsWindow { Name = "2", TickedCodes = new[] { "0039" } }
            };

            var findings = DiagnosticsReport.Describe(snapshot);

            Assert.That(findings.Any(x =>
                    x.Contains("Window 2") && x.Contains("NPC dialogue") && x.Contains("cutscene dialogue")),
                Is.True);
        }

        [Test]
        public void ReportCarriesTheFactsBehindTheWords()
        {
            var snapshot = Snapshot(Reading(codesReadLive: new[] { "003D" }));
            snapshot.AppVersion = "v1.0.4";
            snapshot.ReferenceIndex = "en -> ru, 201267 lines, rules v5, revision abc1234";
            snapshot.LogPath = @"C:\Users\Someone\AppData\Roaming\TataruHelper\Log.txt";
            snapshot.Windows = new[]
            {
                new DiagnosticsWindow
                {
                    Name = "1",
                    Engine = "GoogleTranslate",
                    FromLanguage = "English",
                    ToLanguage = "Russian",
                    TickedCodes = new[] { "0039", "003D", "0044" }
                }
            };

            var report = DiagnosticsReport.Build(snapshot, new DateTime(2026, 8, 10, 14, 3, 0));

            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain("2026-08-10 14:03:00"));
                Assert.That(report, Does.Contain("v1.0.4"));
                Assert.That(report, Does.Contain("ffxiv_dx11.exe"));
                Assert.That(report, Does.Contain("revision abc1234"));
                Assert.That(report, Does.Contain("GoogleTranslate"));
                Assert.That(report, Does.Contain("English -> Russian"));
                Assert.That(report, Does.Contain(@"AppData\Roaming\TataruHelper\Log.txt"));
            });
        }

        // A character's name is readable once the memory reading works, and
        // reports get pasted into public channels.
        [Test]
        public void ReportDoesNotCarryTheCharactersName()
        {
            var report = DiagnosticsReport.Build(
                Snapshot(Reading(codesReadLive: new[] { "003D" })), DateTime.Now);

            Assert.That(report, Does.Not.Contain("Character name"));
            Assert.That(report, Does.Contain("Character read"));
        }

        private static DiagnosticsSnapshot Snapshot(GameReadingDiagnostics reading)
        {
            return new DiagnosticsSnapshot { Reading = reading };
        }

        private static GameReadingDiagnostics Reading(
            bool attached = true,
            bool playerResolved = true,
            bool realtimeEnabled = true,
            string[] codesReadLive = null)
        {
            return new GameReadingDiagnostics(
                attached,
                "ffxiv_dx11.exe  PID: 4242",
                "en",
                playerResolved,
                realtimeEnabled,
                codesReadLive?.Length ?? 0,
                codesReadLive ?? Array.Empty<string>());
        }
    }
}
