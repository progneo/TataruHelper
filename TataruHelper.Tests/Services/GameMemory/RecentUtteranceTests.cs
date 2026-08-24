using System;

using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    /// <summary>
    /// Measured off a duty: the same sentence reached the reader twice, once
    /// from the subtitle strip with nobody named and once from the dialogue
    /// window with the speaker in front of it, and both were shown.
    /// </summary>
    [TestFixture]
    public class RecentUtteranceTests
    {
        private static readonly DateTime Start = new DateTime(2026, 8, 24, 15, 31, 38, DateTimeKind.Utc);

        private const string Line = "i haven't the faintest what's going on, but you'd best keep moving!";

        [Test]
        public void TheFirstTimeWordsAreSaid_TheyAreNotAnEcho()
        {
            Assert.That(new RecentUtterance().IsEcho(Line, "", Start), Is.False);
        }

        /// <summary>
        /// The pair from the log: 15:31:38 bare, 15:31:39 with the speaker.
        /// Compared without him they are one utterance, so only one goes out.
        /// </summary>
        [Test]
        public void TheOtherWindowsCopyAMomentLater_IsAnEcho()
        {
            var recent = new RecentUtterance();
            recent.IsEcho(Line, "", Start);

            Assert.That(recent.IsEcho(Line, "Cassard", Start.AddSeconds(1)), Is.True);
        }

        /// <summary>
        /// Two guards can both say "Halt!". The same words long after are
        /// somebody saying them again, and swallowing those loses lines.
        /// </summary>
        [Test]
        public void TheSameWordsLongAfter_AreSaidAgain()
        {
            var recent = new RecentUtterance();
            recent.IsEcho(Line, "", Start);

            Assert.That(recent.IsEcho(Line, "", Start + RecentUtterance.SameBreath), Is.False);
        }

        [Test]
        public void DifferentWordsInTheSameBreath_AreNotAnEcho()
        {
            var recent = new RecentUtterance();
            recent.IsEcho(Line, "", Start);

            Assert.That(recent.IsEcho("come, ghun gun! our friends need help!", "", Start.AddMilliseconds(50)), Is.False);
        }

        /// <summary>
        /// A run of copies must not hold the gate shut: the moment is counted
        /// from when the words went out, not from the last copy of them.
        /// </summary>
        [Test]
        public void AStreamOfCopies_DoesNotPushTheMomentAlong()
        {
            var recent = new RecentUtterance();
            recent.IsEcho(Line, "", Start);

            for (var at = 100; at < 2000; at += 100)
            {
                Assert.That(recent.IsEcho(Line, "", Start.AddMilliseconds(at)), Is.True, "still the same breath");
            }

            Assert.That(recent.IsEcho(Line, "", Start.AddMilliseconds(2100)), Is.False,
                "past the breath it is said again, whatever came in between");
        }

        [Test]
        public void NothingSaid_IsNeverAnEcho()
        {
            var recent = new RecentUtterance();

            Assert.That(recent.IsEcho(string.Empty, "", Start), Is.False);
            Assert.That(recent.IsEcho(null, "", Start.AddMilliseconds(10)), Is.False);
        }

        [Test]
        public void AClockThatWentBackwards_IsNotTakenForTheSameBreath()
        {
            var recent = new RecentUtterance();
            recent.IsEcho(Line, "", Start);

            Assert.That(recent.IsEcho(Line, "", Start.AddHours(-1)), Is.False);
        }

        [Test]
        public void AfterForgetting_TheWordsAreNewAgain()
        {
            var recent = new RecentUtterance();
            recent.IsEcho(Line, "", Start);
            recent.Forget();

            Assert.That(recent.IsEcho(Line, "", Start.AddMilliseconds(10)), Is.False);
        }

        /// <summary>
        /// The case the narrowing exists for. Two characters saying the same
        /// short thing in the same breath are two utterances - they are named,
        /// and named differently. Only a copy that names nobody is the other
        /// window's.
        /// </summary>
        [Test]
        public void TheSameWordsFromAnotherNamedSpeaker_AreNotAnEcho()
        {
            var recent = new RecentUtterance();
            recent.IsEcho("understood.", "Cid", Start);

            Assert.That(recent.IsEcho("understood.", "Yda", Start.AddMilliseconds(200)), Is.False);
        }

        [Test]
        public void TheSameSpeakerSayingItAgainInTheSameBreath_IsAnEcho()
        {
            var recent = new RecentUtterance();
            recent.IsEcho("understood.", "Cid", Start);

            Assert.That(recent.IsEcho("understood.", "Cid", Start.AddMilliseconds(200)), Is.True);
        }
    }
}