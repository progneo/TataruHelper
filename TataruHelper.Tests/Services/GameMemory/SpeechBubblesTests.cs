using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    /// <summary>
    /// Replayed from a duty on 24 August. The bubble addon held five at once,
    /// four of them left over from characters who had long stopped speaking,
    /// and the reader was asked for the longest - so for two minutes it
    /// answered with a passer-by's line while the player watched three other
    /// bubbles come and go.
    /// </summary>
    [TestFixture]
    public class SpeechBubblesTests
    {
        private const string Stale = "I haven't the faintest what's going on, but you'd best keep moving!";

        private static readonly string[] Standing =
        {
            "Magnus! We're in your debt!", "Be careful, yes?", Stale, "Come and visit us again!"
        };

        private static string[] With(string spoken)
        {
            var all = new string[Standing.Length + 1];
            all[0] = spoken;
            Standing.CopyTo(all, 1);
            return all;
        }

        [Test]
        public void TheBubbleThatJustAppeared_IsTheOneSaid()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(With("True peace...shall reign..."));

            Assert.That(bubbles.Pick(With("Darkness...must be destroyed...")),
                Is.EqualTo("Darkness...must be destroyed..."));
        }

        /// <summary>
        /// The whole of the fault in one assertion: the longest bubble is not
        /// the spoken one, and it stands there sweep after sweep.
        /// </summary>
        [Test]
        public void TheLongestStandingBubble_IsNotAnswered()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(With("True peace...shall reign..."));

            var said = bubbles.Pick(With("Darkness...must be destroyed..."));

            Assert.That(said, Is.Not.EqualTo(Stale));
        }

        [Test]
        public void WhenNothingIsNew_NobodyHasSaidAnything()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(With("True peace...shall reign..."));
            bubbles.Pick(With("Darkness...must be destroyed..."));

            Assert.That(bubbles.Pick(With("Darkness...must be destroyed...")), Is.Empty);
        }

        /// <summary>
        /// Measured off a duty: the first bubble of the run was held back and
        /// the player read nothing until it happened to come round again
        /// thirty seconds later. The addon is only read when it holds
        /// something, so a first look is usually a line genuinely being said.
        /// </summary>
        [Test]
        public void TheFirstLook_IsNotHeldBack()
        {
            Assert.That(new SpeechBubbles().Pick(new[] { "True peace...shall reign..." }),
                Is.EqualTo("True peace...shall reign..."));
        }

        [Test]
        public void ABubbleThatComesBackLater_IsSaidAgain()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(With("True peace...shall reign..."));
            bubbles.Pick(Standing);

            Assert.That(bubbles.Pick(With("True peace...shall reign...")),
                Is.EqualTo("True peace...shall reign..."));
        }

        /// <summary>
        /// Two characters can speak over each other, and then the longer of
        /// the two new ones is the better guess at what is meant to be read.
        /// </summary>
        [Test]
        public void WhenTwoAppearAtOnce_TheLongerIsAnswered()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(Standing);

            Assert.That(
                bubbles.Pick(new[] { "Halt!", "Stand aside, if you value your life!" }),
                Is.EqualTo("Stand aside, if you value your life!"));
        }

        [Test]
        public void AnEmptyScreen_SaysNothing()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(Standing);

            Assert.That(bubbles.Pick(new string[0]), Is.Empty);
            Assert.That(bubbles.Pick(null), Is.Empty);
        }

        [Test]
        public void AfterForgetting_TheScreenIsLookedAtAfresh()
        {
            var bubbles = new SpeechBubbles();
            bubbles.Pick(With("True peace...shall reign..."));
            bubbles.Forget();

            Assert.That(bubbles.Pick(With("Darkness...must be destroyed...")),
                Is.EqualTo(Stale),
                "with nothing remembered every bubble is new, and the longest is the guess");
        }
    }
}
