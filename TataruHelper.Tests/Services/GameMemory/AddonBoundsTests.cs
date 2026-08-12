using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    /// <summary>
    /// The arithmetic that turns what the client keeps into a rectangle a
    /// translation can be drawn into. The client keeps the window's position
    /// and the interface scale in one place and the unscaled size of the node
    /// it draws into in another, so neither alone says how big the box is.
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
    }
}
