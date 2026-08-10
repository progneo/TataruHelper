using NUnit.Framework;

using Translation.Providers.AI;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// A local server is described by its owner as an address and a port, not as
    /// a path to the chat-completions route. Whatever they type has to reach the
    /// right URL, because a 404 from their own machine tells them nothing about
    /// what was wrong with it.
    /// </summary>
    [TestFixture]
    public class OpenAIChatClientEndpointTests
    {
        private const string Fallback = "https://api.example.com/v1/chat/completions";

        [Test]
        public void NothingConfigured_UsesTheProviderDefault()
        {
            Assert.That(OpenAIChatClient.ResolveEndpoint(null, Fallback), Is.EqualTo(Fallback));
            Assert.That(OpenAIChatClient.ResolveEndpoint("   ", Fallback), Is.EqualTo(Fallback));
        }

        [Test]
        public void BareAddress_GetsTheWholeApiPath()
        {
            Assert.That(
                OpenAIChatClient.ResolveEndpoint("http://localhost:11434", Fallback),
                Is.EqualTo("http://localhost:11434/v1/chat/completions"));
        }

        [Test]
        public void TrailingSlashAndSurroundingSpace_AreIgnored()
        {
            Assert.That(
                OpenAIChatClient.ResolveEndpoint("  http://localhost:1234/  ", Fallback),
                Is.EqualTo("http://localhost:1234/v1/chat/completions"));
        }

        [Test]
        public void AddressEndingInV1_GetsOnlyTheRoute()
        {
            Assert.That(
                OpenAIChatClient.ResolveEndpoint("http://localhost:1234/v1", Fallback),
                Is.EqualTo("http://localhost:1234/v1/chat/completions"));
        }

        [Test]
        public void FullEndpoint_IsLeftAlone()
        {
            const string full = "http://localhost:11434/v1/chat/completions";

            Assert.That(OpenAIChatClient.ResolveEndpoint(full, Fallback), Is.EqualTo(full));
        }

        /// <summary>
        /// Some servers sit behind a prefix ("/ollama", "/api/openai"), so the
        /// route is completed onto whatever path was given rather than replacing
        /// it.
        /// </summary>
        [Test]
        public void AddressWithItsOwnPrefix_KeepsThePrefix()
        {
            Assert.That(
                OpenAIChatClient.ResolveEndpoint("https://box.lan/ollama", Fallback),
                Is.EqualTo("https://box.lan/ollama/v1/chat/completions"));
        }
    }
}
