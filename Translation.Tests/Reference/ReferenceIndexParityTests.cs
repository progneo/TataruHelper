using System;
using System.IO;
using System.Linq;

using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    /// <summary>
    /// Runs the builder over a real xivrus export, when one is at hand.
    ///
    /// The rules were worked out against the whole export rather than against
    /// examples, so a handful of unit tests can agree while the counts drift.
    /// Explicit because it wants a gigabyte of files and half a minute.
    ///
    ///   dotnet test --filter FullyQualifiedName~ReferenceIndexParity
    ///     -e XIVRUS_EXPORT=path\to\xiv_ru_weblate-main
    /// </summary>
    [TestFixture, Explicit]
    public class ReferenceIndexParityTests
    {
        [Test]
        public void WholeExport_YieldsTheExpectedCounts()
        {
            var root = Environment.GetEnvironmentVariable("XIVRUS_EXPORT");
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            {
                Assert.Ignore("XIVRUS_EXPORT is not set to an export folder.");
            }

            // The same read the command line does, so what is measured here is
            // what a release is built from.
            var builder = ReferenceIndexUpdater.ReadExportFolder(root, "en", "ru", null);

            TestContext.Out.WriteLine($"sheets   : {builder.Sheets}");
            TestContext.Out.WriteLine($"lines    : {builder.Lines.Count}");
            TestContext.Out.WriteLine($"patterns : {builder.Patterns.Count}");
            TestContext.Out.WriteLine($"speakers : {builder.Speakers.Count}");
            TestContext.Out.WriteLine($"gendered : {builder.Gendered.Count / 2}");
            TestContext.Out.WriteLine($"gen.pat. : {builder.GenderedPatterns.Count / 2}");
            TestContext.Out.WriteLine($"skipped  : {builder.SkippedForMarkup}");
            TestContext.Out.WriteLine($"conflicts: {builder.Conflicts}");

            // Counts from the export this was developed against. Lines and
            // speakers match the earlier python builder exactly.
            //
            // Patterns no longer do: that builder, like this one until the
            // game's own language became the key, only took a line whose two
            // sides both named the player. English and Russian come from the
            // same source and name them in much the same places, so the loss
            // looked small - 576 lines. On a German client it was 2 657, and on
            // a Japanese one 3 697, because those languages address the player
            // somewhere else. Whichever side carries the name, the other is a
            // fixed string and the line can still be found, so both shapes are
            // patterns now: 3 120 here rather than 2 951, and 169 fewer lines
            // thrown away for markup.
            //
            // The hyphenation points and hard spaces the game writes into German
            // move these barely at all - English has neither - but they are what
            // was throwing away the Crystal Exarch on a German client.
            //
            // Speakers went down by four and patterns by three when rows stopped
            // being read across a <target/>. Those were not lines lost: they were
            // lines carrying the next row's text, and four of them were somebody
            // else's name.
            //
            // Recognising the player's name in its bare form as well as its
            // wrapped one stopped 138 more lines being thrown away as markup:
            // patterns 3 117 to 3 200, gendered patterns 957 to 1 005. Lines
            // do not move for that, and should not - a line carrying the name
            // was never stored as a line, it was skipped.
            //
            // Reading <var 1F> as the hyphen it is drawn as took lines from
            // 201 924 to 201 918. Diffing the two indexes key by key, rather
            // than trusting the totals: 819 keys went, 813 arrived, and 813 of
            // the departures are the same key with its hyphens restored -
            // Kan-E-Senna, Raya-O-Senna, Radz-at-Han, Heaven-on-High. Five of
            // the remaining six merged into a key that was already there,
            // spelled with real hyphens by some other row, and carrying word
            // for word the same translation. The sixth was "//".
            //
            // Gendered does not, and deliberately: python resolved the player's
            // name before asking about gender, so a line carrying both landed
            // among the gendered ones with the name still punched out - roughly
            // nine hundred rows that nothing could ever match, since the screen
            // has a name where the key has a placeholder. Those are counted as
            // skipped here instead. Reaching them properly means keeping a
            // pattern per gender, which is a thing to do, not a thing done.
            Assert.Multiple(() =>
            {
                Assert.That(builder.Sheets, Is.EqualTo(2681), "sheets");
                Assert.That(builder.Lines.Count, Is.EqualTo(201918), "lines");
                Assert.That(builder.Patterns.Count, Is.EqualTo(3200), "patterns");
                Assert.That(builder.Speakers.Count, Is.EqualTo(4245), "speakers");
                Assert.That(builder.Gendered.Count / 2, Is.EqualTo(6465), "gendered");

                // Lines that name the character and agree with them at once.
                // They reached neither store before: the gender branch gave up
                // on the name, the pattern branch gave up on the gender, and
                // 984 of the game's most personal lines went to an engine.
                Assert.That(builder.GenderedPatterns.Count / 2, Is.EqualTo(1005), "gendered patterns");
                Assert.That(builder.SkippedForMarkup, Is.EqualTo(5552), "skipped for markup");
            });

            // Lines seen in game, each of which cost a round of investigation.
            Assert.Multiple(() =>
            {
                Assert.That(builder.Lines["The wood... It's watching, you know!"],
                    Is.EqualTo("Лес... Он бдит, знаешь ли!"));
                Assert.That(builder.Lines["O mournful voice of creation! O mournful voice of time!"],
                    Is.EqualTo("О, скорбный голос созидания! О, скорбный глас времён!"));
                Assert.That(builder.Speakers["Mother Miounne"], Is.EqualTo("Матушка Миунна"));
                Assert.That(builder.Speakers.ContainsKey("???"), Is.False);

                // The line that showed the bare name was going unread. It is a
                // pattern, not a line: the placeholder is where the game writes
                // whoever is playing.
                Assert.That(
                    builder.Patterns["Now then, " + ReferenceIndexBuilder.PlayerPlaceholder +
                                     ", I ask that you give unto us your oath of allegiance, " +
                                     "in whatever fashion you see fit."],
                    Is.EqualTo("А теперь, " + ReferenceIndexBuilder.PlayerPlaceholder +
                               ", прошу тебя принести присягу верности — в вольной форме."));
                // Stored as the sheet writes it. The game draws the name
                // capitalised, which is why the reader looks it up NOCASE.
                Assert.That(builder.Speakers["serpent personnel officer"],
                    Is.EqualTo("Кадровый офицер Ордена"));

                // The hyphens are the whole point of this one: the export
                // writes them as a tag, and the screen says Kan-E-Senna.
                Assert.That(
                    builder.Lines["Elder Seedseer Kan-E-Senna is the supreme commander of our forces. " +
                                  "Under her wise leadership, we protect the people of Gridania and " +
                                  "the sanctity of the Twelveswood."],
                    Is.EqualTo("Верховная Жрица Кан-Э-Сенна — верховный командир нашей армии. " +
                               "Под её мудрым началом мы защищаем народ Гридании и Священный Лес."));
            });
        }
    }
}
