using NUnit.Framework;

using Translation.OutgoingChat;

namespace Translation.Tests
{
    [TestFixture]
    public class OutgoingMessageComposerTests
    {
        private OutgoingMessageComposer _composer;

        [SetUp]
        public void SetUp()
        {
            _composer = new OutgoingMessageComposer(new MessageSanitizer(), new ChannelPrefixFormatter());
        }

        [Test]
        public void Compose_NoOptions_ReturnsSanitizedTranslationOnly()
        {
            var result = _composer.Compose("Hello", "Привет", ChatChannel.Party, null, null);
            Assert.That(result, Is.EqualTo("Hello"));
        }

        [Test]
        public void Compose_PrependChannelCommand_AddsPartyPrefix()
        {
            var options = new OutgoingMessageComposeOptions { PrependChannelCommand = true };
            var result = _composer.Compose("Hello", "Привет", ChatChannel.Party, null, options);
            Assert.That(result, Is.EqualTo("/p Hello"));
        }

        [Test]
        public void Compose_AppendOriginal_AddsParenthesizedOriginal()
        {
            var options = new OutgoingMessageComposeOptions { AppendOriginalInParentheses = true };
            var result = _composer.Compose("Hello", "Привет", ChatChannel.Party, null, options);
            Assert.That(result, Is.EqualTo("Hello (Привет)"));
        }

        [Test]
        public void Compose_BothOptions_PrefixThenBodyThenOriginal()
        {
            var options = new OutgoingMessageComposeOptions
            {
                PrependChannelCommand = true, AppendOriginalInParentheses = true
            };
            var result = _composer.Compose("Hello", "Привет", ChatChannel.Party, null, options);
            Assert.That(result, Is.EqualTo("/p Hello (Привет)"));
        }

        [Test]
        public void Compose_AppendOriginal_SkipsDuplicateOriginal()
        {
            var options = new OutgoingMessageComposeOptions { AppendOriginalInParentheses = true };
            var result = _composer.Compose("Hello", "Hello", ChatChannel.Say, null, options);
            Assert.That(result, Is.EqualTo("Hello"));
        }

        [Test]
        public void Compose_Tell_IncludesTellTarget()
        {
            var options = new OutgoingMessageComposeOptions { PrependChannelCommand = true };
            var result = _composer.Compose("Hi there", "Привет", ChatChannel.Tell, "Firstname Lastname@Gilgamesh",
                options);
            Assert.That(result, Is.EqualTo("/t Firstname Lastname@Gilgamesh Hi there"));
        }

        [Test]
        public void Compose_NullOptions_TreatedAsAllFalse()
        {
            var result = _composer.Compose("Hello", "Привет", ChatChannel.Party, null, null);
            Assert.That(result, Is.EqualTo("Hello"));
        }
    }
}