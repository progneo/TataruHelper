using FFXIVTataruHelper;

using NUnit.Framework;

namespace TataruHelper.Tests.Models
{
    // Deciding to take a whole section off the settings page, so the cases
    // that must not hide it matter more than the one that must. Everything
    // uncertain - no game language, no windows yet, a window with no language
    // settled - leaves it alone; only a definite "every window reads what the
    // game already says" takes it away.
    [TestFixture]
    public class ReferenceTranslationUseTests
    {
        [Test]
        public void ReadingAnotherLanguage_IsTheWholePoint()
        {
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("en", new[] { "ru" }), Is.True);
        }

        [Test]
        public void ReadingWhatTheGameAlreadySays_IsNothingToLookUp()
        {
            // The line on screen is already the line wanted. The engine layer
            // answers nothing here, so the switch and the Update button are
            // offering a gigabyte that would never be read.
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("en", new[] { "en" }), Is.False);
        }

        [Test]
        public void CaseAlone_IsNotADifference()
        {
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("EN", new[] { "en" }), Is.False);
        }

        [Test]
        public void OneWindowOutOfSeveral_IsEnoughToKeepIt()
        {
            // The switch is one setting for the whole application, so it is
            // worth having as long as any window would use it.
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("en", new[] { "en", "ru" }), Is.True);
        }

        [Test]
        public void AGermanClientReadInGerman_IsAlsoNothingToLookUp()
        {
            // Nothing here is about English in particular.
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("de", new[] { "de" }), Is.False);
        }

        [Test]
        public void AnUnknownGameLanguage_KeepsIt()
        {
            // The configuration could not be read, or the game has never been
            // run on this machine. Hiding a working feature on that is a guess.
            Assert.That(ReferenceTranslationUse.AnythingToLookUp(string.Empty, new[] { "en" }), Is.True);
        }

        [Test]
        public void NoWindowsYet_KeepsIt()
        {
            // The saved windows arrive on a background thread. Before they do,
            // there is nothing to judge on - and the section blinking out and
            // back in as settings load would be worse than leaving it.
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("en", new string[0]), Is.True);
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("en", null), Is.True);
        }

        [Test]
        public void AWindowWithNoLanguageSettled_KeepsIt()
        {
            Assert.That(ReferenceTranslationUse.AnythingToLookUp("en", new[] { "en", string.Empty }), Is.True);
        }
    }
}
