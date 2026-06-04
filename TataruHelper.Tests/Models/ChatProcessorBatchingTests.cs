using System.Collections.Generic;

using FFXIVTataruHelper;

using NUnit.Framework;

namespace TataruHelper.Tests
{
    [TestFixture]
    public class ChatProcessorBatchingTests
    {
        [Test]
        public void TrySplitBatchedTranslation_ReturnsSegments_WhenCountMatches()
        {
            var result = ChatProcessor.TrySplitBatchedTranslation(
                "one<<<D>>>two<<<D>>>three",
                "<<<D>>>",
                3,
                out List<string> segments);

            Assert.That(result, Is.True);
            Assert.That(segments, Is.EqualTo(new[] { "one", "two", "three" }));
        }

        [Test]
        public void TrySplitBatchedTranslation_ReturnsFalse_WhenCountMismatch()
        {
            var result = ChatProcessor.TrySplitBatchedTranslation(
                "one<<<D>>>two",
                "<<<D>>>",
                3,
                out _);

            Assert.That(result, Is.False);
        }

        [Test]
        public void TrySplitBatchedTranslation_ReturnsFalse_WhenTranslationIsEmpty()
        {
            var result = ChatProcessor.TrySplitBatchedTranslation(
                string.Empty,
                "<<<D>>>",
                1,
                out _);

            Assert.That(result, Is.False);
        }
    }
}