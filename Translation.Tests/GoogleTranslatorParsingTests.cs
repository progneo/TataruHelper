using NUnit.Framework;

using Translation.Providers.Google;

namespace Translation.Tests
{
    [TestFixture]
    public class GoogleTranslatorParsingTests
    {
        [Test]
        public void ParseGoogleJsonTranslation_ReturnsConcatenatedText()
        {
            var body = "[[[\"Hello \",\"こんにちは\",null,null,10],[\"world\",\"世界\",null,null,10]],null,\"ja\"]";

            var result = GoogleTranslator.ParseGoogleJsonTranslation(body);

            Assert.That(result, Is.EqualTo("Hello world"));
        }

        [Test]
        public void ParseGoogleJsonTranslation_ReturnsEmpty_WhenRootArrayIsMissing()
        {
            var result = GoogleTranslator.ParseGoogleJsonTranslation("{\"ok\":true}");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseGoogleHtmlTranslation_ReturnsDecodedLegacyResult()
        {
            var body = "<html><body><div dir=\"ltr\" class=\"t0\">Tom &amp; Jerry</div></body></html>";

            var result = GoogleTranslator.ParseGoogleHtmlTranslation(body);

            Assert.That(result, Is.EqualTo("Tom & Jerry"));
        }

        [Test]
        public void ParseGoogleHtmlTranslation_ReturnsEmpty_WhenContainerMissing()
        {
            var result = GoogleTranslator.ParseGoogleHtmlTranslation("<html><body>No result</body></html>");

            Assert.That(result, Is.Empty);
        }
    }
}