using NUnit.Framework;
using Translation.Providers.DeepL;

namespace Translation.Tests
{
    [TestFixture]
    public class DeepLTranslatorParsingTests
    {
        [Test]
        public void ParseTranslation_ReturnsTextFromResult()
        {
            var body = "{\"jsonrpc\":\"2.0\",\"id\":8350001000,\"result\":{\"texts\":[{\"alternatives\":[]," +
                       "\"text\":\"こんにちは世界\"}],\"lang\":\"EN\",\"lang_is_confident\":true}}";

            var result = DeepLTranslator.ParseTranslation(body);

            Assert.That(result, Is.EqualTo("こんにちは世界"));
        }

        [Test]
        public void ParseTranslation_ConcatenatesMultipleTexts()
        {
            var body = "{\"result\":{\"texts\":[{\"text\":\"Hello \"},{\"text\":\"world\"}]}}";

            var result = DeepLTranslator.ParseTranslation(body);

            Assert.That(result, Is.EqualTo("Hello world"));
        }

        [Test]
        public void ParseTranslation_ReturnsEmpty_WhenResultMissing()
        {
            var result = DeepLTranslator.ParseTranslation("{\"jsonrpc\":\"2.0\",\"error\":{\"code\":-32600}}");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void ParseTranslation_ReturnsEmpty_WhenBodyIsNotJson()
        {
            var result = DeepLTranslator.ParseTranslation("<html>Too many requests</html>");

            Assert.That(result, Is.Empty);
        }

        [Test]
        public void AdjustTimestamp_AlignsToLetterICountPlusOne()
        {
            // "limit" has two 'i' letters, so the timestamp must be divisible by 3.
            var result = DeepLTranslator.AdjustTimestamp("limit", 1_700_000_000_001);

            Assert.That(result % 3, Is.Zero);
            Assert.That(result, Is.GreaterThanOrEqualTo(1_700_000_000_001));
        }

        [Test]
        public void AdjustTimestamp_ReturnsUnchanged_WhenTextHasNoLetterI()
        {
            var result = DeepLTranslator.AdjustTimestamp("hello", 1_700_000_000_001);

            Assert.That(result, Is.EqualTo(1_700_000_000_001));
        }

        [Test]
        public void BuildRequestBody_UsesExtraSpacing_WhenIdMatchesFingerprintRule()
        {
            // 13 % 13 == 0 → the "method" key gets spaces on both sides of the colon.
            var body = DeepLTranslator.BuildRequestBody("hi", "auto", "EN", 13, 1000);

            Assert.That(body, Does.Contain("\"method\" : \"LMT_handle_texts\""));
        }

        [Test]
        public void BuildRequestBody_UsesSingleSpace_WhenIdDoesNotMatchFingerprintRule()
        {
            var body = DeepLTranslator.BuildRequestBody("hi", "auto", "EN", 14, 1000);

            Assert.That(body, Does.Contain("\"method\": \"LMT_handle_texts\""));
            Assert.That(body, Does.Not.Contain("\"method\" : "));
        }

        [Test]
        public void BuildRequestBody_IncludesLanguagesTextAndTimestamp()
        {
            var body = DeepLTranslator.BuildRequestBody("Bonjour", "auto", "EN", 14, 4242);

            Assert.That(body, Does.Contain("\"source_lang_user_selected\":\"auto\""));
            Assert.That(body, Does.Contain("\"target_lang\":\"EN\""));
            Assert.That(body, Does.Contain("\"text\":\"Bonjour\""));
            Assert.That(body, Does.Contain("\"timestamp\":4242"));
        }
    }
}