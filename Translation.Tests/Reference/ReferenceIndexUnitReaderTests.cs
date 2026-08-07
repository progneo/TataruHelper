using System.Linq;

using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    // The rows of a sheet used to be picked out with a regular expression, and
    // it made two kinds of mistake that reading XML cannot: it ran past a row
    // that had no translation into the next row's, and it ignored any row whose
    // target carried an attribute the expression had not been told to expect.
    // These hold the reader to the behaviour that replaced it.
    [TestFixture]
    public class ReferenceIndexUnitReaderTests
    {
        private static string Xliff(string body)
        {
            return "<?xml version=\"1.0\" encoding=\"UTF-8\"?>" +
                   "<xliff version=\"1.2\"><file original=\"x\" datatype=\"plaintext\" " +
                   "source-language=\"en\" target-language=\"ru\"><body>" + body +
                   "</body></file></xliff>";
        }

        private static ReferenceIndexBuilder.Unit[] Read(string body)
        {
            var units = ReferenceIndexBuilder.ReadUnits(Xliff(body), out var malformed);

            Assert.That(malformed, Is.False, "the document is well formed");

            return units.ToArray();
        }

        [Test]
        public void ARowWithNoTranslation_EndsWhereItEnds()
        {
            // The failure that cost 1 281 wrong translations: with nothing
            // between <target/> and the next row, an expression that could
            // cross the boundary filed row 60's text under row 59.
            var units = Read(
                "<trans-unit id=\"59\"><source>59</source><target/></trans-unit>" +
                "<trans-unit id=\"60\"><source>60</source><target state=\"final\">Он ещё жив!?</target></trans-unit>");

            Assert.That(units.Select(x => x.Id), Is.EqualTo(new[] { "60" }));
            Assert.That(units[0].Text, Is.EqualTo("Он ещё жив!?"));
        }

        [Test]
        public void AnAttributeNobodyAskedAbout_DoesNotHideTheRow()
        {
            // The expression matched a target with no attributes or with
            // exactly state="..."; anything else and the row went unread.
            // 1 252 rows in the export sat behind that.
            var units = Read(
                "<trans-unit id=\"1\" approved=\"yes\" xml:space=\"preserve\">" +
                "<source>1</source><target state=\"final\" some-day-added=\"yes\">Здравствуй.</target>" +
                "</trans-unit>");

            Assert.That(units.Single().Text, Is.EqualTo("Здравствуй."));
        }

        [Test]
        public void AMachineLeveragedRow_IsLeftOut()
        {
            // The one qualifier the export uses is "leveraged-mt". The index is
            // meant to hold what people wrote, and the whole feature is showing
            // that instead of a machine's answer.
            var units = Read(
                "<trans-unit id=\"1\"><source>1</source>" +
                "<target state=\"needs-translation\" state-qualifier=\"leveraged-mt\">Машинный текст.</target>" +
                "</trans-unit>" +
                "<trans-unit id=\"2\"><source>2</source><target state=\"final\">Людской текст.</target></trans-unit>");

            Assert.That(units.Select(x => x.Id), Is.EqualTo(new[] { "2" }));
        }

        [Test]
        public void TheOrderRowsAreWrittenIn_IsTheOrderTheyComeBack()
        {
            // Two rows can reduce to the same line, and the first is the one
            // kept. Handing these back as a dictionary would decide that by
            // whatever order the dictionary felt like.
            var units = Read(
                "<trans-unit id=\"7\"><source>7</source><target state=\"final\">семь</target></trans-unit>" +
                "<trans-unit id=\"3\"><source>3</source><target state=\"final\">три</target></trans-unit>" +
                "<trans-unit id=\"5\"><source>5</source><target state=\"final\">пять</target></trans-unit>");

            Assert.That(units.Select(x => x.Id), Is.EqualTo(new[] { "7", "3", "5" }));
        }

        [Test]
        public void EscapedMarkup_ComesBackAsTheCharactersItStandsFor()
        {
            // The game's own tags are written escaped, and every rule after
            // this one reads them as "<var ...>".
            var units = Read(
                "<trans-unit id=\"1\"><source>1</source>" +
                "<target state=\"final\">Kan&lt;var 1F /var&gt;E&amp;co &quot;x&quot;</target></trans-unit>");

            Assert.That(units.Single().Text, Is.EqualTo("Kan<var 1F /var>E&co \"x\""));
        }

        [Test]
        public void SpaceInsideARow_IsKept()
        {
            // The sheets say xml:space="preserve", and a line's spacing is part
            // of the line we have to match against the screen.
            var units = Read(
                "<trans-unit id=\"1\" xml:space=\"preserve\"><source>1</source>" +
                "<target state=\"final\">  два  пробела  </target></trans-unit>");

            Assert.That(units.Single().Text, Is.EqualTo("  два  пробела  "));
        }

        [Test]
        public void AFileThatStopsBeingXml_KeepsWhatWasReadFirst()
        {
            // Half a sheet of hand-made translation beats none, and the caller
            // counts these so an export full of them is noticed.
            var units = ReferenceIndexBuilder.ReadUnits(
                "<xliff><file><body>" +
                "<trans-unit id=\"1\"><source>1</source><target state=\"final\">Первая.</target></trans-unit>" +
                "<trans-unit id=\"2\"><source>2</source><target state=\"final\">Вторая.",
                out var malformed);

            Assert.Multiple(() =>
            {
                Assert.That(malformed, Is.True);
                Assert.That(units.Select(x => x.Id), Is.EqualTo(new[] { "1" }));
            });
        }

        [Test]
        public void NothingAtAll_IsNoRowsAndNoComplaint()
        {
            Assert.Multiple(() =>
            {
                Assert.That(ReferenceIndexBuilder.ReadUnits(null, out var nullMalformed), Is.Empty);
                Assert.That(nullMalformed, Is.False);
                Assert.That(ReferenceIndexBuilder.ReadUnits(string.Empty, out var emptyMalformed), Is.Empty);
                Assert.That(emptyMalformed, Is.False);
            });
        }
    }
}
