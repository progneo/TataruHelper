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
            TestContext.Out.WriteLine($"skipped  : {builder.SkippedForMarkup}");

            // Counts from the export this was developed against. Lines,
            // patterns and speakers match the earlier python builder exactly.
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
                Assert.That(builder.Lines.Count, Is.EqualTo(201837), "lines");
                Assert.That(builder.Patterns.Count, Is.EqualTo(2951), "patterns");
                Assert.That(builder.Speakers.Count, Is.EqualTo(4249), "speakers");
                Assert.That(builder.Gendered.Count / 2, Is.EqualTo(6464), "gendered");
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
            });
        }
    }
}
