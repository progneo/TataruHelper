using NUnit.Framework;

using Translation.OutgoingChat;

namespace Translation.Tests
{
    [TestFixture]
    public class MessageSanitizerTests
    {
        private MessageSanitizer _sanitizer;

        [SetUp]
        public void SetUp()
        {
            _sanitizer = new MessageSanitizer();
        }

        [Test]
        public void Sanitize_NullOrEmpty_ReturnsEmpty()
        {
            Assert.That(_sanitizer.Sanitize(null), Is.EqualTo(string.Empty));
            Assert.That(_sanitizer.Sanitize(string.Empty), Is.EqualTo(string.Empty));
        }

        [Test]
        public void Sanitize_PreservesPrintableAsciiAndCommonWhitespace()
        {
            Assert.That(_sanitizer.Sanitize("Hello, world!\tline\nnext\r\n"),
                Is.EqualTo("Hello, world!\tline\nnext\r\n"));
        }

        [Test]
        public void Sanitize_StripsControlCharacters()
        {
            var input = "abcde";
            Assert.That(_sanitizer.Sanitize(input), Is.EqualTo("abcde"));
        }

        [Test]
        public void Sanitize_PreservesCjkAndEmoji()
        {
            var input = "こんにちは 你好 😀";
            Assert.That(_sanitizer.Sanitize(input), Is.EqualTo(input));
        }

        [Test]
        public void Utf8ByteLength_CountsBytesNotChars()
        {
            Assert.That(_sanitizer.Utf8ByteLength("abc"), Is.EqualTo(3));
            Assert.That(_sanitizer.Utf8ByteLength("こんにちは"), Is.EqualTo(15));
            Assert.That(_sanitizer.Utf8ByteLength("😀"), Is.EqualTo(4));
            Assert.That(_sanitizer.Utf8ByteLength(null), Is.EqualTo(0));
        }
    }
}