using NUnit.Framework;

using Translation.Providers.AI;

namespace Translation.Tests.Providers
{
    /// <summary>
    /// A model told not to reason separately sometimes writes its reasoning into
    /// the visible answer anyway. Dropping that is easy; dropping it without
    /// eating the game's own markup is the part worth pinning, because FFXIV
    /// chat is full of angle brackets the translation must keep.
    /// </summary>
    [TestFixture]
    public class AiResponseSanitizerTests
    {
        [Test]
        public void LeakedReasoning_IsDropped()
        {
            Assert.That(
                AiResponseSanitizer.StripWrappingArtifacts(
                    "<thinking>The speaker is a merchant.</thinking>\nДобро пожаловать."),
                Is.EqualTo("Добро пожаловать."));
        }

        [Test]
        public void EmoteTags_AreKept()
        {
            const string line = "</salute> Приветствую, командир!";

            Assert.That(AiResponseSanitizer.StripWrappingArtifacts(line), Is.EqualTo(line));
        }

        [Test]
        public void AutoTranslateBrackets_AreKept()
        {
            const string line = "【Привет】 <se.1> идём?";

            Assert.That(AiResponseSanitizer.StripWrappingArtifacts(line), Is.EqualTo(line));
        }

        /// <summary>
        /// If the reasoning is all there is, there is no translation behind it -
        /// returning the reasoning would put it on screen as if it were one.
        /// </summary>
        [Test]
        public void ReasoningWithNothingAfterIt_IsNotMistakenForTheAnswer()
        {
            Assert.That(
                AiResponseSanitizer.StripWrappingArtifacts("<thinking>Hmm, tricky.</thinking>"),
                Is.EqualTo("<thinking>Hmm, tricky.</thinking>"));
        }

        [Test]
        public void CodeFencesAndQuotes_AreStillStripped()
        {
            Assert.That(
                AiResponseSanitizer.StripWrappingArtifacts("```\nДобро пожаловать.\n```"),
                Is.EqualTo("Добро пожаловать."));
            Assert.That(
                AiResponseSanitizer.StripWrappingArtifacts("\"Добро пожаловать.\""),
                Is.EqualTo("Добро пожаловать."));
        }
    }
}
