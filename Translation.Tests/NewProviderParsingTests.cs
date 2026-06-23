using NUnit.Framework;

using Translation.Providers.AI;
using Translation.Providers.Azure;
using Translation.Providers.DeepL;
using Translation.Providers.GoogleCloud;

namespace Translation.Tests
{
    [TestFixture]
    public class NewProviderParsingTests
    {
        [Test]
        public void Azure_ParsesTranslationText()
        {
            var body =
                @"[{""detectedLanguage"":{""language"":""en"",""score"":1.0},""translations"":[{""text"":""Hallo Welt"",""to"":""de""}]}]";
            var result = AzureTranslator.ParseTranslation(body);
            Assert.That(result, Is.EqualTo("Hallo Welt"));
        }

        [Test]
        public void Azure_EmptyOnInvalidJson()
        {
            Assert.That(AzureTranslator.ParseTranslation("[]"), Is.EqualTo(string.Empty));
        }

        [Test]
        public void GoogleCloud_ParsesTranslatedText()
        {
            var body = @"{""data"":{""translations"":[{""translatedText"":""Bonjour le monde""}]}}";
            var result = GoogleCloudTranslator.ParseTranslation(body);
            Assert.That(result, Is.EqualTo("Bonjour le monde"));
        }

        [Test]
        public void DeepLApi_ConcatenatesAllTranslations()
        {
            var body =
                @"{""translations"":[{""detected_source_language"":""EN"",""text"":""Hallo""},{""detected_source_language"":""EN"",""text"":"" Welt""}]}";
            var result = DeepLApiTranslator.ParseTranslation(body);
            Assert.That(result, Is.EqualTo("Hallo Welt"));
        }

        [Test]
        public void OpenAIChat_ExtractsMessageContent()
        {
            var body =
                @"{""id"":""x"",""choices"":[{""index"":0,""message"":{""role"":""assistant"",""content"":""Привет, мир!""}}]}";
            var result = OpenAIChatClient.ParseContent(body);
            Assert.That(result, Is.EqualTo("Привет, мир!"));
        }
    }
}