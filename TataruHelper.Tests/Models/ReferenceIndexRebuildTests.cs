using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Update;

using NUnit.Framework;

namespace TataruHelper.Tests.Models
{
    // Pressing update can replace the translations with ones for a different
    // pair of languages, and once did: the game had been restarted in English
    // while the application still believed it German, so the button fetched a
    // German index over the working English one. Minutes, hundreds of
    // megabytes, and the translations stopped working with nothing to explain
    // it. It is a question now, not a surprise.
    [TestFixture]
    public class ReferenceIndexRebuildTests
    {
        private static ReferenceIndexState Installed(string source, string reading)
        {
            return new ReferenceIndexState(true, source, reading, "abc1234", 201838);
        }

        [Test]
        public void TheSamePair_IsNotWorthAsking()
        {
            Assert.That(ReferenceIndexRebuild.ChangesLanguages(Installed("en", "ru"), "en", "ru"), Is.False);
        }

        [Test]
        public void AnotherGameLanguage_IsAskedAbout()
        {
            Assert.That(ReferenceIndexRebuild.ChangesLanguages(Installed("en", "ru"), "de", "ru"), Is.True);
        }

        [Test]
        public void AnotherReadingLanguage_IsAskedAbout()
        {
            Assert.That(ReferenceIndexRebuild.ChangesLanguages(Installed("en", "ru"), "en", "de"), Is.True);
        }

        [Test]
        public void NothingInstalled_IsNothingToLose()
        {
            // A fresh install has no translations to replace, and a question
            // there would only be in the way.
            var empty = new ReferenceIndexState(false, string.Empty, string.Empty, string.Empty, 0);

            Assert.That(ReferenceIndexRebuild.ChangesLanguages(empty, "en", "ru"), Is.False);
        }

        [Test]
        public void CaseAlone_IsNotADifference()
        {
            Assert.That(ReferenceIndexRebuild.ChangesLanguages(Installed("en", "ru"), "EN", "RU"), Is.False);
        }

        [Test]
        public void TheQuestion_NamesBothPairs()
        {
            // Which one is there now and which one it would become: without
            // both, the answer is a guess.
            var question = ReferenceIndexRebuild.Question(
                Installed("en", "ru"), "de", "ru", _ => "have {0}, fetch {1}");

            Assert.That(question, Is.EqualTo("have en → ru, fetch de → ru"));
        }
    }
}
