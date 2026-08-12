using System;
using System.Windows;

using FFXIVTataruHelper.Services.UI;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.UI
{
    /// <summary>
    /// Measured off the running game: between one line and the next the game's
    /// dialogue window is gone for a frame or two, because it is torn down and
    /// rebuilt rather than reused. Without the wait the copy blinked out and
    /// back on every line of every conversation.
    /// </summary>
    [TestFixture]
    public class DialogueOverlayHoldTests
    {
        private static readonly DateTime Start = new DateTime(2026, 8, 12, 18, 13, 29, DateTimeKind.Utc);

        private static readonly Rect Box = new Rect(1210, 1143, 1020, 270);

        [Test]
        public void WhatIsFoundNow_IsWhatIsDrawn()
        {
            var hold = new DialogueOverlayHold();

            Assert.That(hold.Decide(true, Box, Start, out var drawn), Is.True);
            Assert.That(drawn, Is.EqualTo(Box));
        }

        [Test]
        public void TheGapBetweenTwoLines_IsRiddenOut()
        {
            var hold = new DialogueOverlayHold();
            hold.Decide(true, Box, Start, out _);

            var shown = hold.Decide(false, Rect.Empty, Start.AddMilliseconds(80), out var drawn);

            Assert.That(shown, Is.True, "the copy should stay put across the changeover");
            Assert.That(drawn, Is.EqualTo(Box), "and stay where it was, rather than jump");
        }

        [Test]
        public void AConversationThatHasEnded_ClearsTheCopy()
        {
            var hold = new DialogueOverlayHold();
            hold.Decide(true, Box, Start, out _);

            Assert.That(
                hold.Decide(false, Rect.Empty, Start + DialogueOverlayHold.Grace, out _),
                Is.False);
        }

        /// <summary>
        /// The wait starts again from the last sighting, not from the first, or
        /// a long conversation would eventually run it out mid-flow.
        /// </summary>
        [Test]
        public void EachSighting_StartsTheWaitAgain()
        {
            var hold = new DialogueOverlayHold();
            hold.Decide(true, Box, Start, out _);
            hold.Decide(true, Box, Start.AddSeconds(30), out _);

            Assert.That(hold.Decide(false, Rect.Empty, Start.AddSeconds(30.1), out _), Is.True);
        }

        [Test]
        public void OnceClearedItIsNotHeld()
        {
            var hold = new DialogueOverlayHold();
            hold.Decide(true, Box, Start, out _);
            hold.Clear();

            Assert.That(hold.Decide(false, Rect.Empty, Start.AddMilliseconds(10), out _), Is.False);
        }

        [Test]
        public void WithNothingEverSeen_NothingIsDrawn()
        {
            Assert.That(
                new DialogueOverlayHold().Decide(false, Rect.Empty, Start, out _),
                Is.False);
        }
    }
}
