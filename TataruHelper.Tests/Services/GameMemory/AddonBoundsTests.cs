using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    /// <summary>
    /// The arithmetic that turns what the client keeps into a rectangle a
    /// translation can be drawn into. The place comes already worked out; only
    /// the size has to be, since the node keeps the size it was designed at and
    /// the scale it is drawn at separately.
    /// </summary>
    [TestFixture]
    public class AddonBoundsTests
    {
        [Test]
        public void TheSizeIsTheDesignSizeAtTheInterfaceScale()
        {
            var bounds = AddonBounds.From(100, 800, 700, 200, 1.5f);

            Assert.That(bounds.IsKnown, Is.True);
            Assert.That(bounds.X, Is.EqualTo(100f));
            Assert.That(bounds.Y, Is.EqualTo(800f));
            Assert.That(bounds.Width, Is.EqualTo(1050f));
            Assert.That(bounds.Height, Is.EqualTo(300f));
        }

        /// <summary>
        /// Measured off the running game: Leandryne's dialogue box at an
        /// interface scale of 150%, read as the client had worked it out.
        ///
        /// The window's own stated position for the same box was 1380,1188 -
        /// 170 and 45 further along, being where it would sit unscaled, since
        /// the client grows a window about its middle. Taking that as the place
        /// to draw put the copy visibly to the right of the box it was meant to
        /// cover, and nothing about it would have looked wrong at 100%.
        /// </summary>
        [Test]
        public void TheBoxFromTheRunningGame_IsWhereItWasDrawn()
        {
            var bounds = AddonBounds.From(1210f, 1143f, 680, 180, 1.5f);

            Assert.That(bounds.X, Is.EqualTo(1210f));
            Assert.That(bounds.Y, Is.EqualTo(1143f));
            Assert.That(bounds.Width, Is.EqualTo(1020f));
            Assert.That(bounds.Height, Is.EqualTo(270f));
        }

        [Test]
        public void AtTheDefaultScale_TheSizeIsTheDesignSize()
        {
            var bounds = AddonBounds.From(0, 0, 700, 200, 1f);

            Assert.That(bounds.Width, Is.EqualTo(700f));
            Assert.That(bounds.Height, Is.EqualTo(200f));
        }

        /// <summary>
        /// The game's dialogue box can be dragged anywhere, including off the
        /// left edge, and the client keeps that as a negative. A rectangle is
        /// still a rectangle there.
        /// </summary>
        [Test]
        public void AWindowDraggedPastTheEdge_KeepsItsNegativeCorner()
        {
            var bounds = AddonBounds.From(-40, -12, 700, 200, 1f);

            Assert.That(bounds.IsKnown, Is.True);
            Assert.That(bounds.X, Is.EqualTo(-40f));
            Assert.That(bounds.Y, Is.EqualTo(-12f));
        }

        /// <summary>
        /// Nothing read means nothing said. A box of no size would put the
        /// translation in the top-left corner of the screen, which is worse
        /// than leaving it where it already was.
        /// </summary>
        [TestCase((ushort)0, (ushort)200, 1f, TestName = "no width")]
        [TestCase((ushort)700, (ushort)0, 1f, TestName = "no height")]
        [TestCase((ushort)700, (ushort)200, 0f, TestName = "no scale")]
        [TestCase((ushort)700, (ushort)200, -1f, TestName = "impossible scale")]
        [TestCase((ushort)700, (ushort)200, float.NaN, TestName = "unreadable scale")]
        public void NothingWorthReading_IsNotARectangle(ushort width, ushort height, float scale)
        {
            var bounds = AddonBounds.From(100, 800, width, height, scale);

            Assert.That(bounds.IsKnown, Is.False);
        }

        [Test]
        public void APlaceThatReadsAsNothing_IsNotARectangle()
        {
            Assert.That(AddonBounds.From(float.NaN, 800, 700, 200, 1f).IsKnown, Is.False);
            Assert.That(AddonBounds.From(100, float.NaN, 700, 200, 1f).IsKnown, Is.False);
        }
    }
}
