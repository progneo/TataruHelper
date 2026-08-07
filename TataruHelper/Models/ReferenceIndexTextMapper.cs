using System;
using System.Globalization;

using FFXIVTataruHelper.Services.Update;

using Translation.Reference;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// Puts what the reference index is doing into the sentence the user reads.
    ///
    /// Kept apart from the view model because it is the part worth being sure
    /// of: a status line that says the wrong thing about an eighty-megabyte
    /// download is how somebody presses the button twice.
    /// </summary>
    public static class ReferenceIndexTextMapper
    {
        public static string Describe(ReferenceIndexState state, Func<string, string> localize)
        {
            if (!state.IsInstalled)
            {
                return localize("ReferenceIndexMissing");
            }

            var lines = Count(state.Lines);

            // Both languages, not just the one it reads into. An index keyed on
            // English is silent on a German client, and this line is the only
            // place that would ever say why.
            var languages = state.SourceLanguage.Length > 0
                ? state.SourceLanguage + " → " + state.Language
                : state.Language;

            // An index built from a folder carries no revision, so there is
            // nothing to name until an update has been through here.
            return state.Revision.Length > 0
                ? string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexInstalled"),
                    lines, languages, ShortRevision(state.Revision))
                : string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexInstalledUnknownRevision"),
                    lines, languages);
        }

        /// <summary>
        /// What the daily check found, in a line the user did not ask for and
        /// so has to be able to make sense of on its own.
        ///
        /// Each of these ends by saying what to press, because none of them
        /// does anything by itself: the check asks the project a cheap
        /// question, and fetching an answer of a gigabyte stays the user's
        /// decision unless they have said otherwise.
        /// </summary>
        /// <returns>Empty when there is nothing worth saying.</returns>
        public static string Describe(
            ReferenceIndexCheckOutcome outcome,
            ReferenceIndexState state,
            string gameLanguage,
            string readingLanguage,
            Func<string, string> localize)
        {
            switch (outcome)
            {
                case ReferenceIndexCheckOutcome.Missing:
                    return localize("ReferenceIndexMissing");

                case ReferenceIndexCheckOutcome.LanguagesChanged:
                    return string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexWrongLanguage"),
                        state.SourceLanguage + " → " + state.Language,
                        gameLanguage + " → " + readingLanguage);

                case ReferenceIndexCheckOutcome.RulesChanged:
                    return localize("ReferenceIndexRulesChanged");

                case ReferenceIndexCheckOutcome.UnknownRevision:
                    return localize("ReferenceIndexRevisionUnrecorded");

                case ReferenceIndexCheckOutcome.RevisionChanged:
                    return localize("ReferenceIndexNewRevision");

                case ReferenceIndexCheckOutcome.UpToDate:
                    return localize("ReferenceIndexUpToDate");

                default:
                    return string.Empty;
            }
        }

        public static string Describe(ReferenceUpdateProgress report, Func<string, string> localize)
        {
            if (report.Stage != ReferenceUpdateStage.Downloading)
            {
                return localize("ReferenceIndexWriting");
            }

            var megabytes = Count(report.Bytes / (1024 * 1024));

            // The archive is read while it arrives, so both numbers belong in
            // one sentence. Told separately, the line count looked like a second
            // stage that had started before the download was done.
            return report.Lines > 0
                ? string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexDownloadingWithLines"),
                    megabytes, Count(report.Lines))
                : string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexDownloading"), megabytes);
        }

        public static string Describe(ReferenceUpdateResult result, Func<string, string> localize)
        {
            switch (result.Outcome)
            {
                case ReferenceUpdateOutcome.AlreadyCurrent:
                    return localize("ReferenceIndexUpToDate");

                case ReferenceUpdateOutcome.Updated:
                    return string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexUpdated"),
                        Count(result.Lines));

                default:
                    return string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexFailed"), result.Detail);
            }
        }

        /// <summary>Two hundred thousand lines are easier to read in groups.</summary>
        private static string Count(long value)
        {
            return value.ToString("N0", CultureInfo.CurrentCulture);
        }

        /// <summary>As much of a commit as anybody quotes.</summary>
        private static string ShortRevision(string revision)
        {
            return revision.Substring(0, Math.Min(7, revision.Length));
        }
    }
}
