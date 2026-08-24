using FFXIVTataruHelper.Services.UI;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.UI
{
    /// <summary>
    /// The translation comes back a moment after the line was said, and in that
    /// moment the game moves on. The copy then put the last thing said into the
    /// mouth of the box the game was drawing then, so the line is what the copy
    /// is checked on. This is the question, kept apart from the window that
    /// shows the copy.
    /// </summary>
    [TestFixture]
    public class DialogueOverlayLineCheckTests
    {
        [Test]
        public void TheLineTheGameIsDrawing_IsCurrent()
        {
            var shownKey = DialogueOverlayLineCheck.KeyOf("Cid:This is the line on screen");

            Assert.That(
                DialogueOverlayLineCheck.IsCurrent(shownKey, "Cid:This is the line on screen"),
                Is.True,
                "the copy belongs on the line the game is still drawing");
        }

        [Test]
        public void AnotherLineTheGameHasDrawn_IsStale()
        {
            var shownKey = DialogueOverlayLineCheck.KeyOf("Cid:This is the line on screen");

            Assert.That(
                DialogueOverlayLineCheck.IsCurrent(shownKey, "Yda:Quite."),
                Is.False,
                "the game has moved on, and the last thing said is not the thing it is drawing");
        }

        [Test]
        public void NothingToCheckOn_IsCurrent()
        {
            Assert.That(
                DialogueOverlayLineCheck.IsCurrent(string.Empty, "Cid:Quite."),
                Is.True,
                "a line nobody has named cannot yet be taken for a stale one");
        }

        [Test]
        public void ALineThatCannotBeReadOff_IsCurrent()
        {
            var shownKey = DialogueOverlayLineCheck.KeyOf("Cid:Quite.");

            Assert.That(
                DialogueOverlayLineCheck.IsCurrent(shownKey, string.Empty),
                Is.True,
                "an unreadable screen is not evidence the copy is stale, only that nobody can say");
        }

        [Test]
        public void WhoSaidItAndHowItIsSet_IsNotChecked_OnlyTheWordsAre()
        {
            var shownKey = DialogueOverlayLineCheck.KeyOf("Cid:Understood.");

            Assert.That(
                DialogueOverlayLineCheck.IsCurrent(shownKey, "cid:  Understood."),
                Is.True,
                "the line read off the screen and the line through the translator render the words the same");
        }

        [Test]
        public void TheKeyOfASpeakerLine_IsTheWordsWithoutTheSpeaker()
        {
            Assert.That(
                DialogueOverlayLineCheck.KeyOf("  Cid:Understood.  "),
                Is.EqualTo("understood."),
                "who said it does not make one line another");
        }
    }
}