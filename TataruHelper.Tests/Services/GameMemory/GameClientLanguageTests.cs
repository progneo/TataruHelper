using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    // The index of hand-made translations is keyed on the language the game
    // draws its dialogue in, so that language has to be read off the game. The
    // alternatives were both tried and both wrong: the window's setting is a
    // translation preference and may say "work it out", and working it out from
    // a line means a German player in chat decides what the next line of
    // dialogue is.
    [TestFixture]
    public class GameClientLanguageTests
    {
        // The file is a plain list of "name<tab>value" lines.
        private static string Config(params string[] lines)
        {
            return string.Join("\r\n", lines) + "\r\n";
        }

        [Test]
        public void EachLanguageTheGameIsPublishedIn_IsRecognised()
        {
            Assert.Multiple(() =>
            {
                Assert.That(GameClientLanguage.Parse(Config("Language\t0")), Is.EqualTo("ja"));
                Assert.That(GameClientLanguage.Parse(Config("Language\t1")), Is.EqualTo("en"));
                Assert.That(GameClientLanguage.Parse(Config("Language\t2")), Is.EqualTo("de"));
                Assert.That(GameClientLanguage.Parse(Config("Language\t3")), Is.EqualTo("fr"));
            });
        }

        [Test]
        public void TheLanguageIsFoundAmongTheOtherSettings()
        {
            var configuration = Config(
                "<FFXIV.cfg>",
                "Version\t1",
                "Region\t2",
                "Language\t2",
                "Fps\t1");

            Assert.That(GameClientLanguage.Parse(configuration), Is.EqualTo("de"));
        }

        [Test]
        public void ASettingThatMerelyStartsWithLanguage_IsNotMistakenForIt()
        {
            // Nothing in the file is called that today, but a "LanguageFoo"
            // added later must not be read as the language itself.
            var configuration = Config("LanguageSomethingElse\t3", "Language\t1");

            Assert.That(GameClientLanguage.Parse(configuration), Is.EqualTo("en"));
        }

        [Test]
        public void AConfigurationWithoutALanguage_SaysNothing()
        {
            // Empty rather than a guess. Something that does not know the
            // language leaves the index to try, which is the safe way round.
            Assert.That(GameClientLanguage.Parse(Config("Fps\t1")), Is.Empty);
            Assert.That(GameClientLanguage.Parse(string.Empty), Is.Empty);
            Assert.That(GameClientLanguage.Parse(null), Is.Empty);
        }

        [Test]
        public void TheMemoryReaderIsToldTheSameLanguage()
        {
            // It was told "English" whatever the client was set to, which is
            // the wrong signatures and the wrong text for three of the four.
            Assert.Multiple(() =>
            {
                Assert.That(GameClientLanguage.ReaderName("ja"), Is.EqualTo("Japanese"));
                Assert.That(GameClientLanguage.ReaderName("de"), Is.EqualTo("German"));
                Assert.That(GameClientLanguage.ReaderName("fr"), Is.EqualTo("French"));
                Assert.That(GameClientLanguage.ReaderName("en"), Is.EqualTo("English"));

                // Unreadable configuration leaves it where it was, rather than
                // making the reader worse than it was before it asked.
                Assert.That(GameClientLanguage.ReaderName(string.Empty), Is.EqualTo("English"));
            });
        }

        [Test]
        public void ALanguageTheGameDoesNotHave_SaysNothing()
        {
            // Chinese and Korean clients are published by other companies and
            // are not what this numbering counts.
            Assert.That(GameClientLanguage.Parse(Config("Language\t9")), Is.Empty);
            Assert.That(GameClientLanguage.Parse(Config("Language\tzz")), Is.Empty);
        }
    }
}
