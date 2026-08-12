using System;
using System.Collections.Generic;

using NUnit.Framework;

using Translation.Reference;

using Pattern = Translation.Reference.SqliteReferenceTranslationSource.ItemPattern;

namespace Translation.Tests.Reference
{
    /// <summary>
    /// A shopkeeper says the same sentence whichever thing she is selling, and
    /// the game writes the name in as it draws. The export holds one line with
    /// a hole where the name goes, so the line has to be recognised by what
    /// stands on either side of that hole.
    ///
    /// This is the line that prompted it, from G'jusana: hand-translated in the
    /// export, and reaching the screen machine-translated because the hole made
    /// the two sides look like different lines.
    /// </summary>
    [TestFixture]
    public class ItemPatternMatchingTests
    {
        private const string Hole = "\u0002";

        private static readonly Pattern Gjusana = new Pattern(
            "As I mentioned earlier, I'm willing to part with each book for the discounted price of 100 ",
            ".",
            "Как я уже говорила, я готова расстаться с каждой книгой по сниженной цене в 100 " + Hole + ".");

        [Test]
        public void TheNameTheGameWroteIn_IsCarriedAcross()
        {
            var line = "As I mentioned earlier, I'm willing to part with each book for the " +
                       "discounted price of 100 Allagan tomestones of poetics.";

            Assert.That(
                SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { Gjusana }, line, out var ru),
                Is.True);
            Assert.That(ru, Is.EqualTo(
                "Как я уже говорила, я готова расстаться с каждой книгой по сниженной цене в 100 " +
                "Allagan tomestones of poetics."));
        }

        [Test]
        public void AnotherItemInTheSameSentence_MatchesJustAsWell()
        {
            var line = "As I mentioned earlier, I'm willing to part with each book for the " +
                       "discounted price of 100 Allagan tomestones of causality.";

            SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { Gjusana }, line, out var ru);

            Assert.That(ru, Does.EndWith("100 Allagan tomestones of causality."));
        }

        [Test]
        public void ADifferentSentence_IsNotMatched()
        {
            var line = "Each book can be purchased from G'jusana.";

            Assert.That(
                SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { Gjusana }, line, out _),
                Is.False);
        }

        /// <summary>
        /// A hole has to swallow something. The two fixed halves with nothing
        /// between them are a different sentence, and answering with this one
        /// would put a translation on screen that was never written for it.
        /// </summary>
        [Test]
        public void TheFixedPartsAlone_AreNotThisLine()
        {
            var line = "As I mentioned earlier, I'm willing to part with each book for the " +
                       "discounted price of 100 .";

            Assert.That(
                SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { Gjusana }, line, out _),
                Is.False);
        }

        /// <summary>
        /// Two patterns can both fit. The one that pins down more of the line is
        /// the one that meant it - otherwise a short, loose pattern answers for
        /// sentences that a longer one describes exactly.
        /// </summary>
        [Test]
        public void WhenTwoFit_TheMoreParticularOneAnswers()
        {
            var loose = new Pattern("You receive ", ".", "Вы получаете " + Hole + ".");
            var exact = new Pattern("You receive 100 ", " from the vendor.",
                "Торговец выдаёт вам 100 " + Hole + ".");

            var line = "You receive 100 Allagan tomestones of poetics from the vendor.";

            SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { loose, exact }, line, out var ru);

            Assert.That(ru, Is.EqualTo("Торговец выдаёт вам 100 Allagan tomestones of poetics."));
        }

        [Test]
        public void OrderOfThePatterns_DoesNotDecide()
        {
            var loose = new Pattern("You receive ", ".", "Вы получаете " + Hole + ".");
            var exact = new Pattern("You receive 100 ", " from the vendor.",
                "Торговец выдаёт вам 100 " + Hole + ".");

            var line = "You receive 100 Allagan tomestones of poetics from the vendor.";

            SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { exact, loose }, line, out var first);
            SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { loose, exact }, line, out var second);

            Assert.That(first, Is.EqualTo(second));
        }

        [Test]
        public void NothingToMatchAgainst_IsNotAMatch()
        {
            Assert.That(
                SqliteReferenceTranslationSource.TryMatchItemPattern(
                    Array.Empty<Pattern>(), "anything at all", out _),
                Is.False);
            Assert.That(
                SqliteReferenceTranslationSource.TryMatchItemPattern(null, "anything at all", out _),
                Is.False);
            Assert.That(
                SqliteReferenceTranslationSource.TryMatchItemPattern(new[] { Gjusana }, null, out _),
                Is.False);
        }

        /// <summary>
        /// Thirty lines a second pass through a cutscene, and every one that
        /// misses reaches this. It has to cost nothing worth noticing.
        /// </summary>
        [Test]
        public void MatchingAgainstTheWholeSet_IsFastEnoughForACutscene()
        {
            var patterns = new List<Pattern>();
            for (var i = 0; i < 400; i++)
            {
                patterns.Add(new Pattern("Line number " + i + " offers ", " for a price.",
                    "Строка " + i + " предлагает " + Hole + " за деньги."));
            }

            var line = "Line number 399 offers Allagan tomestones of poetics for a price.";

            // Warm up, so the measurement is of the matching and not of the
            // first-call costs around it.
            SqliteReferenceTranslationSource.TryMatchItemPattern(patterns, line, out _);

            var watch = System.Diagnostics.Stopwatch.StartNew();
            for (var i = 0; i < 1000; i++)
            {
                SqliteReferenceTranslationSource.TryMatchItemPattern(patterns, line, out _);
            }
            watch.Stop();

            var perLookupMicroseconds = watch.Elapsed.TotalMilliseconds;
            Assert.That(perLookupMicroseconds, Is.LessThan(200),
                "1000 lookups against 400 patterns should not take 200ms");
        }
    }
}
