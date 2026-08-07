using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

namespace TataruHelper.Tests.ViewModel
{
    // Everything finds a chat window by its number: the sidebar, the delete
    // button, and the binding that ties a window to its saved settings. Two
    // windows sharing a number leaves one of them impossible to select or
    // delete, because every search stops at the first match - which is exactly
    // what a user hit, with a window named "1" that would not go away.
    [TestFixture]
    public class ChatWindowIdsTests
    {
        [Test]
        public void TheFirstWindow_IsZero()
        {
            Assert.That(ChatWindowIds.Next(new long[0]), Is.EqualTo(0));
        }

        [Test]
        public void ANewWindow_TakesOnePastTheHighest()
        {
            Assert.That(ChatWindowIds.Next(new long[] { 0, 1, 2 }), Is.EqualTo(3));
        }

        [Test]
        public void TheHighest_IsNotNecessarilyTheLast()
        {
            // Delete the middle window and add another, and the list is no
            // longer in order. "One past the last" would hand out 2 here,
            // which window 2 already answers to.
            Assert.That(ChatWindowIds.Next(new long[] { 0, 2, 1 }), Is.EqualTo(3));
        }

        [Test]
        public void AGapInTheMiddle_IsLeftAlone()
        {
            // Reusing 1 would be tidier and is not worth the risk: settings
            // saved elsewhere may still name the window that had it.
            Assert.That(ChatWindowIds.Next(new long[] { 0, 2 }), Is.EqualTo(3));
        }
    }
}
