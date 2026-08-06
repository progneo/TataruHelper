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

            // The index the application ships with was built from a folder and
            // carries no revision, so there is nothing to name until an update
            // has been through here.
            return state.Revision.Length > 0
                ? string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexInstalled"),
                    lines, state.Language, ShortRevision(state.Revision))
                : string.Format(CultureInfo.CurrentCulture, localize("ReferenceIndexInstalledUnknownRevision"),
                    lines, state.Language);
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
