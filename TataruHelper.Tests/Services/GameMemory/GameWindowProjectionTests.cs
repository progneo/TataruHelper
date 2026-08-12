using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    /// <summary>
    /// The step from where the game draws a window to where the desktop would
    /// have to be painted for a copy of it to sit on top.
    /// </summary>
    [TestFixture]
    public class GameWindowProjectionTests
    {
        private static readonly AddonBounds DialogueBox = AddonBounds.From(100, 800, 700, 200, 1f);

        /// <summary>
        /// The game filling an unscaled screen is the case where nothing has to
        /// be done, and the one everything else is a departure from.
        /// </summary>
        [Test]
        public void FullScreenAtOneHundredPercent_LeavesThePlaceAsItIs()
        {
            var projection = new GameWindowProjection(0, 0, 1.0);

            Assert.That(projection.TryProject(DialogueBox, out var rect), Is.True);
            Assert.That(rect.Left, Is.EqualTo(100));
            Assert.That(rect.Top, Is.EqualTo(800));
            Assert.That(rect.Width, Is.EqualTo(700));
            Assert.That(rect.Height, Is.EqualTo(200));
        }

        /// <summary>
        /// In a window the game counts from inside its own frame, so where the
        /// frame sits on the desktop has to be added back.
        /// </summary>
        [Test]
        public void InAWindow_TheCornerOfThatWindowIsAddedBack()
        {
            var projection = new GameWindowProjection(300, 150, 1.0);

            projection.TryProject(DialogueBox, out var rect);

            Assert.That(rect.Left, Is.EqualTo(400));
            Assert.That(rect.Top, Is.EqualTo(950));
            Assert.That(rect.Width, Is.EqualTo(700), "the window's corner does not change its size");
        }

        /// <summary>
        /// The game counts real pixels; WPF counts units that are pixels only
        /// at 100%. Forgetting this puts the box half a screen out of place on
        /// a laptop, which is where most people read.
        /// </summary>
        [Test]
        public void OnAScaledDisplay_EverythingIsDividedByTheScale()
        {
            var projection = new GameWindowProjection(0, 0, 1.5);

            projection.TryProject(DialogueBox, out var rect);

            Assert.That(rect.Left, Is.EqualTo(100 / 1.5).Within(0.001));
            Assert.That(rect.Top, Is.EqualTo(800 / 1.5).Within(0.001));
            Assert.That(rect.Width, Is.EqualTo(700 / 1.5).Within(0.001));
            Assert.That(rect.Height, Is.EqualTo(200 / 1.5).Within(0.001));
        }

        [Test]
        public void AWindowedGameOnAScaledDisplay_TakesBothIntoAccount()
        {
            var projection = new GameWindowProjection(300, 150, 2.0);

            projection.TryProject(DialogueBox, out var rect);

            Assert.That(rect.Left, Is.EqualTo(200));
            Assert.That(rect.Top, Is.EqualTo(475));
        }

        [Test]
        public void BeforeTheGameWindowIsFound_NothingIsProjected()
        {
            Assert.That(GameWindowProjection.None.IsUsable, Is.False);
            Assert.That(GameWindowProjection.None.TryProject(DialogueBox, out _), Is.False);
        }

        [Test]
        public void AWindowWhosePlaceIsNotKnown_IsNotProjected()
        {
            var projection = new GameWindowProjection(0, 0, 1.0);

            Assert.That(projection.TryProject(AddonBounds.Unknown, out _), Is.False);
        }

        [Test]
        public void AScaleOfNothing_IsNotDividedBy()
        {
            var projection = new GameWindowProjection(0, 0, 0);

            Assert.That(projection.IsUsable, Is.False);
            Assert.That(projection.TryProject(DialogueBox, out _), Is.False);
        }
    }
}
