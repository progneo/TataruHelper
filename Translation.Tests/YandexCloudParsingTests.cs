using NUnit.Framework;

using Translation.Providers.YandexCloud;

namespace Translation.Tests
{
    [TestFixture]
    public class YandexCloudParsingTests
    {
        [Test]
        public void ParsesFirstTranslationText()
        {
            var body = @"{""translations"":[{""text"":""Привет, мир"",""detectedLanguageCode"":""en""}]}";
            var result = YandexCloudTranslator.ParseTranslation(body);
            Assert.That(result, Is.EqualTo("Привет, мир"));
        }

        [Test]
        public void ReturnsEmpty_OnNoTranslations()
        {
            Assert.That(YandexCloudTranslator.ParseTranslation("{}"), Is.EqualTo(string.Empty));
        }
    }
}