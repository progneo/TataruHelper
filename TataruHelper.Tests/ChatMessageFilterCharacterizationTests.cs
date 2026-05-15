using FFXIVTataruHelper;
using NUnit.Framework;

namespace TataruHelper.Tests
{
    [TestFixture]
    public class ChatMessageFilterCharacterizationTests
    {
        [Test]
        public void ShouldTranslate_ReturnsFalse_ForBlacklistedMessage()
        {
            var filter = new ChatMessageFilter(
                new[] { "Updating online status to Away from Keyboard." },
                new string[0]);

            var shouldTranslate = filter.ShouldTranslate("Updating online status to Away from Keyboard.");

            Assert.That(shouldTranslate, Is.False);
        }

        [Test]
        public void ShouldTranslate_ReturnsTrue_ForRegularMessage()
        {
            var filter = new ChatMessageFilter(
                new[] { "System message" },
                new string[0]);

            var shouldTranslate = filter.ShouldTranslate("Hello from party chat");

            Assert.That(shouldTranslate, Is.True);
        }

        [Test]
        public void TrySplitNickname_SplitsOnlyForConfiguredChatCode()
        {
            var filter = new ChatMessageFilter(
                new string[0],
                new[] { "000A" });

            var split = filter.TrySplitNickname("000A", "Player Name: hello world", out var nickname, out var body);

            Assert.That(split, Is.True);
            Assert.That(nickname, Is.EqualTo("Player Name:"));
            Assert.That(body, Is.EqualTo(" hello world"));
        }

        [Test]
        public void TrySplitNickname_DoesNotSplit_WhenChatCodeNotConfigured()
        {
            var filter = new ChatMessageFilter(
                new string[0],
                new[] { "000A" });

            var split = filter.TrySplitNickname("000B", "Player Name: hello world", out var nickname, out var body);

            Assert.That(split, Is.False);
            Assert.That(nickname, Is.Empty);
            Assert.That(body, Is.EqualTo("Player Name: hello world"));
        }
    }
}
