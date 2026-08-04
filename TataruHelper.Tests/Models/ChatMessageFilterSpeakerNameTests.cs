using FFXIVTataruHelper;

using NUnit.Framework;

namespace TataruHelper.Tests.Models
{
    /// <summary>
    /// Guards the rule that decides whether the text before a colon is a speaker.
    ///
    /// Cutscene subtitles carry no speaker, so a colon inside the sentence used to
    /// be mistaken for the separator: the opening clause went untranslated and was
    /// then rendered in bold as if someone were named that.
    /// </summary>
    [TestFixture]
    public class ChatMessageFilterSpeakerNameTests
    {
        [TestCase("Naoh Gamduhla")]
        [TestCase("Short-tempered Thaumaturge")]
        [TestCase("Kuplo Kopp")]
        [TestCase("???")]
        [TestCase("Вспыльчивый чародей")]
        public void RealNames_AreAccepted(string candidate)
        {
            Assert.That(ChatMessageFilter.LooksLikeSpeakerName(candidate), Is.True);
        }

        [TestCase("For the sake of all, I beseech thee")]
        [TestCase("Ради всех нас, я умоляю тебя")]
        [TestCase("Oh dear. Is that a sword in the stump? Bad idea")]
        [TestCase("Is this our dark stranger?")]
        [TestCase("")]
        [TestCase("   ")]
        public void SentenceFragments_AreRejected(string candidate)
        {
            Assert.That(ChatMessageFilter.LooksLikeSpeakerName(candidate), Is.False);
        }

        [Test]
        public void OverlyLongCandidate_IsRejected()
        {
            Assert.That(ChatMessageFilter.LooksLikeSpeakerName(new string('a', 41)), Is.False);
        }

        [Test]
        public void TooManyWords_AreRejected()
        {
            Assert.That(ChatMessageFilter.LooksLikeSpeakerName("one two three four five six"), Is.False);
        }
    }
}
