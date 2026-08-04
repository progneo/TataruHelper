using NUnit.Framework;

using Translation.Providers.AI;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// Gemini does not answer in the OpenAI chat shape, so its reply is parsed
    /// separately: the text lives under candidates/content/parts, and a model may
    /// split one answer across several parts.
    /// </summary>
    [TestFixture]
    public class GeminiChatClientTests
    {
        [Test]
        public void SinglePart_IsReturned()
        {
            const string body = """
                {"candidates":[{"content":{"parts":[{"text":"Добро пожаловать."}],"role":"model"}}]}
                """;

            Assert.That(GeminiChatClient.ParseContent(body), Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void SplitParts_AreJoined()
        {
            const string body = """
                {"candidates":[{"content":{"parts":[{"text":"Добро "},{"text":"пожаловать."}],"role":"model"}}]}
                """;

            Assert.That(GeminiChatClient.ParseContent(body), Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void BlockedOrEmptyResponse_YieldsNothing()
        {
            const string body = """
                {"candidates":[{"finishReason":"SAFETY"}],"promptFeedback":{"blockReason":"SAFETY"}}
                """;

            Assert.That(GeminiChatClient.ParseContent(body), Is.Empty);
        }

        [TestCase("")]
        [TestCase("   ")]
        [TestCase("{}")]
        public void NoContent_YieldsNothing(string body)
        {
            Assert.That(GeminiChatClient.ParseContent(body), Is.Empty);
        }
    }
}
