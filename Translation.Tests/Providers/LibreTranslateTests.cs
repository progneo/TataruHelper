using NUnit.Framework;

using Translation.Providers.LibreTranslate;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// LibreTranslate is not an LLM: one sentence in, one string out, no prompt.
    /// The parts worth pinning are the two the user can get wrong - the address
    /// they type, and what happens when the instance answers with something
    /// other than a translation.
    /// </summary>
    [TestFixture]
    public class LibreTranslateTests
    {
        [Test]
        public void NothingConfigured_UsesThePublicInstance()
        {
            Assert.That(
                LibreTranslateTranslator.ResolveEndpoint(null),
                Is.EqualTo("https://libretranslate.com/translate"));
        }

        [Test]
        public void BareAddress_GetsTheRoute()
        {
            Assert.That(
                LibreTranslateTranslator.ResolveEndpoint("http://localhost:5000"),
                Is.EqualTo("http://localhost:5000/translate"));
        }

        [Test]
        public void TrailingSlashAndSpace_AreIgnored()
        {
            Assert.That(
                LibreTranslateTranslator.ResolveEndpoint("  http://localhost:5000/  "),
                Is.EqualTo("http://localhost:5000/translate"));
        }

        [Test]
        public void FullAddress_IsLeftAlone()
        {
            Assert.That(
                LibreTranslateTranslator.ResolveEndpoint("https://lt.example.com/translate"),
                Is.EqualTo("https://lt.example.com/translate"));
        }

        [Test]
        public void Translation_IsRead()
        {
            Assert.That(
                LibreTranslateTranslator.ParseContent("""{"translatedText":"Добро пожаловать."}"""),
                Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void AnythingThatIsNotATranslation_IsEmpty()
        {
            Assert.That(LibreTranslateTranslator.ParseContent("""{"error":"Invalid API key"}"""), Is.Empty);
            Assert.That(LibreTranslateTranslator.ParseContent("<html>not json</html>"), Is.Empty);
            Assert.That(LibreTranslateTranslator.ParseContent(""), Is.Empty);
        }
    }
}
