using NUnit.Framework;

using Translation.Providers.AI;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// Anthropic answers with a list of blocks rather than one string, and only
    /// the text ones carry the translation. What else may appear in that list is
    /// the model's business, so anything unfamiliar is skipped rather than
    /// indexed past.
    /// </summary>
    [TestFixture]
    public class ClaudeChatClientTests
    {
        [Test]
        public void SingleTextBlock_IsReturned()
        {
            const string body = """
                {"content":[{"type":"text","text":"Добро пожаловать."}],"stop_reason":"end_turn"}
                """;

            Assert.That(ClaudeChatClient.ParseContent(body), Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void SplitTextBlocks_AreJoined()
        {
            const string body = """
                {"content":[{"type":"text","text":"Добро "},{"type":"text","text":"пожаловать."}]}
                """;

            Assert.That(ClaudeChatClient.ParseContent(body), Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void BlocksThatAreNotText_AreSkipped()
        {
            const string body = """
                {"content":[{"type":"thinking","thinking":""},{"type":"text","text":"Готово."}]}
                """;

            Assert.That(ClaudeChatClient.ParseContent(body), Is.EqualTo("Готово."));
        }

        [Test]
        public void EmptyContent_IsEmpty()
        {
            Assert.That(ClaudeChatClient.ParseContent("""{"content":[],"stop_reason":"refusal"}"""),
                Is.Empty);
        }

        /// <summary>
        /// A declined request is a successful HTTP response with nothing in it.
        /// Retrying it just spends the same request again, so it is told apart
        /// from an empty answer that a retry might fix.
        /// </summary>
        [Test]
        public void Refusal_IsRecognised()
        {
            Assert.That(ClaudeChatClient.IsRefusal("""{"content":[],"stop_reason":"refusal"}"""), Is.True);
            Assert.That(ClaudeChatClient.IsRefusal("""{"content":[],"stop_reason":"end_turn"}"""), Is.False);
            Assert.That(ClaudeChatClient.IsRefusal("not json at all"), Is.False);
            Assert.That(ClaudeChatClient.IsRefusal(""), Is.False);
        }
    }
}
