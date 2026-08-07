using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Update;

using Translation.Reference;

using NUnit.Framework;

namespace TataruHelper.Tests.Models
{
    // The daily check runs with nobody watching, and its answer decides whether
    // a gigabyte comes down. Two of these outcomes look alike and are not: an
    // index that says nothing about its revision is not an index known to be
    // out of date, and an index for the wrong language is not something to
    // replace behind the user's back.
    [TestFixture]
    public class ReferenceIndexAutoCheckTests
    {
        private const int CurrentRules = ReferenceIndexBuilder.RulesVersion;

        private const string Latest = "4f30bd6a1c2e5f8b9d0a3c4e5f6a7b8c9d0e1f2a";

        private static ReferenceIndexState Installed(
            string revision = Latest, string source = "en", string reading = "ru", int rules = CurrentRules)
        {
            return new ReferenceIndexState(true, source, reading, revision, 201_924, rules);
        }

        private static ReferenceIndexCheckOutcome Decide(ReferenceIndexState state, string latest = Latest)
        {
            return ReferenceIndexAutoCheck.Decide(state, "en", "ru", latest);
        }

        [Test]
        public void TheCurrentRevision_IsNothingToReport()
        {
            Assert.That(Decide(Installed()), Is.EqualTo(ReferenceIndexCheckOutcome.UpToDate));
        }

        [Test]
        public void CaseAlone_IsNotANewRevision()
        {
            Assert.That(Decide(Installed(Latest.ToUpperInvariant())),
                Is.EqualTo(ReferenceIndexCheckOutcome.UpToDate));
        }

        [Test]
        public void AProjectThatHasMovedOn_IsWorthFetching()
        {
            var outcome = Decide(Installed("0000000000000000000000000000000000000000"));

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.RevisionChanged));
            Assert.That(ReferenceIndexAutoCheck.MayInstall(outcome), Is.True);
        }

        [Test]
        public void NoIndexAtAll_IsSaidBeforeAnythingElse()
        {
            // A fresh installation that has never fetched anything looks like a
            // working one until a line of dialogue quietly goes to an engine.
            var outcome = Decide(new ReferenceIndexState(false, string.Empty, string.Empty, string.Empty, 0, 0));

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.Missing));
            Assert.That(ReferenceIndexAutoCheck.MayInstall(outcome), Is.True);
        }

        [Test]
        public void OlderParsingRules_AreOutOfDateAtTheCurrentRevision()
        {
            // The whole point of recording the rules: the translation has not
            // moved, but what this build makes of it has, and the revision
            // matching would otherwise answer "nothing to do".
            var outcome = Decide(Installed(rules: CurrentRules - 1));

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.RulesChanged));
            Assert.That(ReferenceIndexAutoCheck.MayInstall(outcome), Is.True);
        }

        [Test]
        public void OlderParsingRules_AreKnownWithoutAskingTheProject()
        {
            // Worth being sure of: if GitHub is unreachable, an index this
            // build knows to be wrong must still be reported.
            Assert.That(Decide(Installed(rules: CurrentRules - 1), string.Empty),
                Is.EqualTo(ReferenceIndexCheckOutcome.RulesChanged));
        }

        [Test]
        public void AnIndexForAnotherLanguage_IsReportedButNeverFetchedUnattended()
        {
            // Fetching it replaces translations that were working with ones for
            // a language the user is not reading, and it costs the whole export
            // to find out. The button asks; a timer has nobody to ask.
            var outcome = ReferenceIndexAutoCheck.Decide(Installed(source: "de"), "en", "ru", Latest);

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.LanguagesChanged));
            Assert.That(ReferenceIndexAutoCheck.MayInstall(outcome), Is.False);
        }

        [Test]
        public void TheWrongLanguage_IsSaidBeforeTheRevision()
        {
            // Both are true of a stale German index, and only one of them
            // explains why nothing on screen is being translated.
            var outcome = ReferenceIndexAutoCheck.Decide(
                Installed("0000000", source: "de"), "en", "ru", Latest);

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.LanguagesChanged));
        }

        [Test]
        public void AnIndexWithNoRevision_SaysSoRatherThanClaimingNewLinesExist()
        {
            // Every release ships an index built from a folder, so this is what
            // a fresh installation finds. Reporting it as "new lines available"
            // would be a guess repeated every day.
            var outcome = Decide(Installed(string.Empty));

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.UnknownRevision));
            Assert.That(ReferenceIndexAutoCheck.MayInstall(outcome), Is.True);
        }

        [Test]
        public void AProjectThatCouldNotBeAsked_IsNotReported()
        {
            // No network is not news. The user did nothing to prompt this
            // check, so an error from it would arrive out of nowhere.
            var outcome = Decide(Installed(), string.Empty);

            Assert.That(outcome, Is.EqualTo(ReferenceIndexCheckOutcome.Unknown));
            Assert.That(ReferenceIndexAutoCheck.IsWorthSaying(outcome), Is.False);
            Assert.That(ReferenceIndexAutoCheck.MayInstall(outcome), Is.False);
        }

        [Test]
        public void BeingUpToDate_IsWorthSayingButNotWorthFetching()
        {
            Assert.That(ReferenceIndexAutoCheck.IsWorthSaying(ReferenceIndexCheckOutcome.UpToDate), Is.True);
            Assert.That(ReferenceIndexAutoCheck.MayInstall(ReferenceIndexCheckOutcome.UpToDate), Is.False);
        }
    }
}
