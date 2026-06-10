using NUnit.Framework;

using Translation.Models;

namespace Translation.Tests
{
    [TestFixture]
    public class TranslationRequestTests
    {
        [Test]
        public void GetHashCode_EqualRequests_ProduceEqualHashes()
        {
            var first = new TranslationRequest("hello", TranslationEngineName.GoogleTranslate, "en", "ja");
            var second = new TranslationRequest("hello", TranslationEngineName.GoogleTranslate, "en", "ja");

            Assert.That(first.GetHashCode(), Is.EqualTo(second.GetHashCode()));
        }

        [Test]
        public void GetHashCode_NullFields_DoesNotThrow()
        {
            var request = new TranslationRequest(null, TranslationEngineName.GoogleTranslate, null, null);

            Assert.That(() => request.GetHashCode(), Throws.Nothing);
        }

        [Test]
        public void Equals_DifferentSentence_ReturnsFalse()
        {
            var first = new TranslationRequest("hello", TranslationEngineName.GoogleTranslate, "en", "ja");
            var second = new TranslationRequest("bye", TranslationEngineName.GoogleTranslate, "en", "ja");

            Assert.That(first, Is.Not.EqualTo(second));
            Assert.That(first != second, Is.True);
        }

        [Test]
        public void EqualityOperator_EqualRequests_ReturnsTrue()
        {
            var first = new TranslationRequest("hello", TranslationEngineName.DeepLApi, "en", "ru");
            var second = new TranslationRequest("hello", TranslationEngineName.DeepLApi, "en", "ru");

            Assert.That(first == second, Is.True);
        }
    }
}