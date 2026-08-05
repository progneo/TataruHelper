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

        // The game destroys and recreates Talk for every line of a conversation,
        // so it comes back at a different address each time. A subtitle still
        // holding the last line of a finished cutscene must not read as fresh
        // dialogue just because the key around it changed - that announced the
        // same stale subtitle between every single line of the conversation.
        [Test]
        public void RecreatedAddon_HoldingUnchangedText_IsNotReportedAgain()
        {
            var reader = CreateReader();
            var stale = "A Light there once was that shone throughout this realm.";

            reader.TrySelectActiveCandidate(
                new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                {
                    Candidate("TalkSubtitle@2000", "0044", string.Empty, stale),
                },
                out _);

            for (int line = 0; line < 3; line++)
            {
                // Each line of the conversation reallocates both addons.
                var address = 0x3000 + (line * 0x100);
                var spoken = "Line " + line;

                Assert.That(
                    reader.TrySelectActiveCandidate(
                        // Subtitle first: the addon list is walked in whatever
                        // order the game holds it, so the stale one gets to be
                        // the first "new" candidate.
                        new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                        {
                            Candidate("TalkSubtitle@" + (address + 8).ToString("X"), "0044", string.Empty, stale),
                            Candidate("Talk@" + address.ToString("X"), "003D", "Cid", spoken),
                        },
                        out var snapshot),
                    Is.True);

                Assert.That(snapshot.TalkText, Is.EqualTo(spoken),
                    "the stale subtitle came back under a new address and was announced again");
            }
        }

        [Test]
        public void TwoBubbles_WithDifferentText_AreBothStillReported()
        {
            var reader = CreateReader();

            reader.TrySelectActiveCandidate(
                new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                {
                    Candidate("_MiniTalk@4000", "003D", "Yda", "Who goes there?"),
                },
                out _);

            Assert.That(
                reader.TrySelectActiveCandidate(
                    new List<(string, TalkAddonRealtimeDialogSnapshot, string)>
                    {
                        Candidate("_MiniTalk@4000", "003D", "Yda", "Who goes there?"),
                        Candidate("_MiniTalk@5000", "003D", "Cid", "Hold."),
                    },
                    out var snapshot),
                Is.True);

            Assert.That(snapshot.TalkText, Is.EqualTo("Hold."));
        }
    }
}
