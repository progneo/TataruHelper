using System.Collections.Generic;

using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    /// <summary>
    /// Covers which addon the reader treats as "currently speaking".
    ///
    /// The Talk addon keeps its last line forever, and a cutscene subtitle clears
    /// itself between lines, so the naive answers all misbehave: taking the first
    /// addon with any text hides subtitles behind a finished conversation, and
    /// comparing against the last reported line makes the two alternate on every
    /// poll.
    /// </summary>
    [TestFixture]
    public class TalkAddonRealtimeReaderSelectionTests
    {
        private const string TalkKey = "Talk@1000";
        private const string SubtitleKey = "TalkSubtitle@2000";

        private static (string, TalkAddonRealtimeDialogSnapshot, string) Candidate(
            string key, string code, string speaker, string text)
        {
            return (key, TalkAddonRealtimeDialogSnapshot.Available(code, speaker, text), text);
        }

        private static TalkAddonRealtimeReader CreateReader() => new TalkAddonRealtimeReader(null);

        [Test]
        public void StaleTalkLine_DoesNotHide_ChangingSubtitle()
        {
            var reader = CreateReader();

            var talk = Candidate(TalkKey, "003D", "Antoinaut", "We hope you have a pleasant stay.");

            // First sweep: everything is new, Talk is seen first.
            reader.TrySelectActiveCandidate(new List<(string, TalkAddonRealtimeDialogSnapshot, string)> { talk },
                out _);

            var withSubtitle = new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
            {
                talk,
                Candidate(SubtitleKey, "0044", string.Empty, "Crystal bearer..."),
            };

            Assert.That(reader.TrySelectActiveCandidate(withSubtitle, out var snapshot), Is.True);
            Assert.That(snapshot.TalkText, Is.EqualTo("Crystal bearer..."));
        }

        [Test]
        public void UnchangedCandidates_KeepReportingTheSameLine()
        {
            var reader = CreateReader();

            var candidates = new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
            {
                Candidate(TalkKey, "003D", "Antoinaut", "We hope you have a pleasant stay."),
                Candidate(SubtitleKey, "0044", string.Empty, "Crystal bearer..."),
            };

            reader.TrySelectActiveCandidate(candidates, out _);
            reader.TrySelectActiveCandidate(candidates, out var first);
            reader.TrySelectActiveCandidate(candidates, out var second);

            // A steady answer is what keeps the gateway from re-emitting.
            Assert.That(second.TalkText, Is.EqualTo(first.TalkText));
        }

        [Test]
        public void SubtitleClearingBetweenLines_DoesNotFallBackToTheTalkAddon()
        {
            var reader = CreateReader();

            var talk = Candidate(TalkKey, "003D", "Antoinaut", "We hope you have a pleasant stay.");

            reader.TrySelectActiveCandidate(new List<(string, TalkAddonRealtimeDialogSnapshot, string)> { talk },
                out _);

            reader.TrySelectActiveCandidate(
                new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                {
                    talk,
                    Candidate(SubtitleKey, "0044", string.Empty, "Crystal bearer..."),
                },
                out _);

            // The subtitle blanks itself; only the stale Talk line is left.
            Assert.That(
                reader.TrySelectActiveCandidate(
                    new List<(string, TalkAddonRealtimeDialogSnapshot, string)> { talk }, out var snapshot),
                Is.True);

            Assert.That(snapshot.TalkText, Is.EqualTo("Crystal bearer..."),
                "the gap between subtitles must not re-announce the finished conversation");
        }

        [Test]
        public void NextSubtitleLine_IsReported()
        {
            var reader = CreateReader();

            var talk = Candidate(TalkKey, "003D", "Antoinaut", "We hope you have a pleasant stay.");

            reader.TrySelectActiveCandidate(
                new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                {
                    talk,
                    Candidate(SubtitleKey, "0044", string.Empty, "Crystal bearer..."),
                },
                out _);

            reader.TrySelectActiveCandidate(
                new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                {
                    talk,
                    Candidate(SubtitleKey, "0044", string.Empty, "I am Hydaelyn. All made one."),
                },
                out var snapshot);

            Assert.That(snapshot.TalkText, Is.EqualTo("I am Hydaelyn. All made one."));
        }

        [Test]
        public void TalkingToAnNpc_IsReported_AfterACutscene()
        {
            var reader = CreateReader();

            reader.TrySelectActiveCandidate(
                new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                {
                    Candidate(SubtitleKey, "0044", string.Empty, "Go now, my child."),
                },
                out _);

            reader.TrySelectActiveCandidate(new List<(string, TalkAddonRealtimeDialogSnapshot, string)>(), out _);

            Assert.That(
                reader.TrySelectActiveCandidate(
                    new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                    {
                        Candidate(TalkKey, "003D", "Adalhard", "Some say she is overbearing."),
                    },
                    out var snapshot),
                Is.True);

            Assert.That(snapshot.TalkText, Is.EqualTo("Some say she is overbearing."));
        }
    }
}
