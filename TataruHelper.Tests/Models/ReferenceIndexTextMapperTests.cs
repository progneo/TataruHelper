using System;
using System.Globalization;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Update;

using NUnit.Framework;

using Translation.Reference;

namespace TataruHelper.Tests.Models
{
    // The status line is all the user has to go on: the download takes minutes,
    // says nothing while it runs unless this does, and replaces the translations
    // they are reading when it finishes.
    [TestFixture]
    public class ReferenceIndexTextMapperTests
    {
        private CultureInfo _culture;

        // The counts are grouped the way the machine groups numbers, which is
        // not the way the machine running these tests necessarily does.
        [SetUp]
        public void SetUp()
        {
            _culture = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        }

        [TearDown]
        public void TearDown()
        {
            CultureInfo.CurrentCulture = _culture;
        }

        // Stands in for the window's dictionary, keeping the assertions about
        // what is said rather than about how it is worded this week.
        private static string Localize(string key)
        {
            switch (key)
            {
                case "ReferenceIndexMissing": return "none";
                case "ReferenceIndexInstalled": return "{0}|{1}|{2}";
                case "ReferenceIndexInstalledUnknownRevision": return "{0}|{1}|unnamed";
                case "ReferenceIndexDownloading": return "downloading {0}";
                case "ReferenceIndexDownloadingWithLines": return "downloading {0}, {1} lines";
                case "ReferenceIndexWriting": return "writing";
                case "ReferenceIndexUpToDate": return "current";
                case "ReferenceIndexUpdated": return "updated {0}";
                case "ReferenceIndexFailed": return "failed: {0}";
                default: return key;
            }
        }

        [Test]
        public void AnInstalledIndex_NamesItsRevisionShort()
        {
            var state = new ReferenceIndexState(true, "en", "ru", "4f30bd6a1c2e5f8b9d0a3c4e5f6a7b8c9d0e1f2a",
                201837);

            Assert.That(Describe(state), Is.EqualTo("201,837|en → ru|4f30bd6"));
        }

        [Test]
        public void TheShippedIndex_SaysItsRevisionIsUnknown()
        {
            // Built from a folder by the release script, so no commit was
            // recorded and the first update cannot be skipped as unnecessary.
            var state = new ReferenceIndexState(true, "en", "ru", string.Empty, 201837);

            Assert.That(Describe(state), Is.EqualTo("201,837|en → ru|unnamed"));
        }

        [Test]
        public void AnIndexForAGermanClient_SaysWhichLanguageItIsKeyedOn()
        {
            // The one thing that explains an index sitting there matching
            // nothing: it answers German, and this window is reading English.
            var state = new ReferenceIndexState(true, "de", "ru", "abc1234def", 600000);

            Assert.That(Describe(state), Is.EqualTo("600,000|de → ru|abc1234"));
        }

        [Test]
        public void NoIndex_SaysSoRatherThanShowingZeroLines()
        {
            var state = new ReferenceIndexState(false, string.Empty, string.Empty, string.Empty, 0);

            Assert.That(Describe(state), Is.EqualTo("none"));
        }

        [Test]
        public void Downloading_CountsInMegabytes()
        {
            // The archive has no declared length, so there is no percentage to
            // show - only how much has come down so far.
            var report = new ReferenceUpdateProgress(ReferenceUpdateStage.Downloading, 268_435_456, 0, 0);

            Assert.That(ReferenceIndexTextMapper.Describe(report, Localize), Is.EqualTo("downloading 256"));
        }

        [Test]
        public void Downloading_SaysWhatHasBeenMadeOfItInTheSameBreath()
        {
            // The archive is read as it arrives. Reported as a separate stage,
            // the line count looked like an install that had begun before the
            // download had finished - which is what it was mistaken for.
            var report = new ReferenceUpdateProgress(ReferenceUpdateStage.Downloading, 268_435_456, 400, 120_000);

            Assert.That(ReferenceIndexTextMapper.Describe(report, Localize),
                Is.EqualTo("downloading 256, 120,000 lines"));
        }

        [Test]
        public void Writing_SaysSo()
        {
            var report = new ReferenceUpdateProgress(ReferenceUpdateStage.Writing, 0, 0, 0);

            Assert.That(ReferenceIndexTextMapper.Describe(report, Localize), Is.EqualTo("writing"));
        }

        [Test]
        public void AnUnchangedProject_IsNotReportedAsAnUpdate()
        {
            var result = new ReferenceUpdateResult(ReferenceUpdateOutcome.AlreadyCurrent, "abc1234", 0);

            Assert.That(ReferenceIndexTextMapper.Describe(result, Localize), Is.EqualTo("current"));
        }

        [Test]
        public void AFinishedUpdate_SaysHowManyLinesItBrought()
        {
            var result = new ReferenceUpdateResult(ReferenceUpdateOutcome.Updated, "abc1234", 203_411);

            Assert.That(ReferenceIndexTextMapper.Describe(result, Localize), Is.EqualTo("updated 203,411"));
        }

        [Test]
        public void AFailure_CarriesItsReason()
        {
            // Whatever went wrong is the only clue the user gets without going
            // to the log, so it is passed through rather than summarised.
            var result = new ReferenceUpdateResult(ReferenceUpdateOutcome.Failed, "Access to the path is denied.", 0);

            Assert.That(ReferenceIndexTextMapper.Describe(result, Localize),
                Is.EqualTo("failed: Access to the path is denied."));
        }

        private static string Describe(ReferenceIndexState state)
        {
            return ReferenceIndexTextMapper.Describe(state, Localize);
        }
    }
}
