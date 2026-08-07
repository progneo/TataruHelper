using System;

using FFXIVTataruHelper.Services.Update;

using Translation.Reference;

namespace FFXIVTataruHelper
{
    /// <summary>What a check of the translation project found.</summary>
    public enum ReferenceIndexCheckOutcome
    {
        /// <summary>
        /// The project could not be asked - no network, or GitHub said no. Not
        /// worth putting on screen: nothing about the translations has changed,
        /// and the next check is a day away.
        /// </summary>
        Unknown,

        /// <summary>The installed lines are the project's current ones.</summary>
        UpToDate,

        /// <summary>
        /// Nothing is installed at all. A fresh installation that has never
        /// fetched anything looks exactly like a working one until a line of
        /// dialogue goes to an engine.
        /// </summary>
        Missing,

        /// <summary>
        /// The lines were read by parsing rules this build has since changed.
        /// The project need not have moved for them to be wrong.
        /// </summary>
        RulesChanged,

        /// <summary>
        /// The installed lines are keyed on a language the game is no longer
        /// played in, so they match nothing on screen.
        /// </summary>
        LanguagesChanged,

        /// <summary>
        /// The installed lines do not say which revision they came from, which
        /// is every index built from a folder - including the one each release
        /// ships with. There is no telling whether they are current.
        /// </summary>
        UnknownRevision,

        /// <summary>The project has written lines since these were built.</summary>
        RevisionChanged
    }

    /// <summary>
    /// Whether the translation project has anything the installed index has
    /// not, and whether the application may go and get it without being asked.
    ///
    /// Asking is cheap - one request to GitHub for the current commit, a few
    /// kilobytes - and fetching is not: the export is around a gigabyte. So the
    /// two are decided separately, and only the asking happens by itself.
    /// </summary>
    public static class ReferenceIndexAutoCheck
    {
        /// <summary>
        /// How often the project is asked. A translation gains lines over
        /// weeks, so anything shorter is asking a question whose answer has not
        /// had time to change.
        /// </summary>
        public static readonly TimeSpan Interval = TimeSpan.FromDays(1);

        /// <param name="latestRevision">
        /// What the project is at, or empty when it could not be asked.
        /// </param>
        public static ReferenceIndexCheckOutcome Decide(
            ReferenceIndexState state, string gameLanguage, string readingLanguage, string latestRevision)
        {
            // Everything below is about telling one index from another, and
            // there is no index.
            if (!state.IsInstalled)
            {
                return ReferenceIndexCheckOutcome.Missing;
            }

            // Said before anything about revisions, because it is the fault
            // that makes the translations silent rather than merely dated, and
            // the only one nothing else on screen would explain.
            if (ReferenceIndexRebuild.ChangesLanguages(state, gameLanguage, readingLanguage))
            {
                return ReferenceIndexCheckOutcome.LanguagesChanged;
            }

            // Known without asking anybody: this build reads the export
            // differently from the build that wrote these lines.
            if (state.RulesVersion != ReferenceIndexBuilder.RulesVersion)
            {
                return ReferenceIndexCheckOutcome.RulesChanged;
            }

            if (string.IsNullOrEmpty(latestRevision))
            {
                return ReferenceIndexCheckOutcome.Unknown;
            }

            // Not the same as "out of date", and worth the separate word: an
            // index of unknown provenance may well be current, and saying that
            // new lines exist when nothing knows that is a lie told daily.
            if (state.Revision.Length == 0)
            {
                return ReferenceIndexCheckOutcome.UnknownRevision;
            }

            return string.Equals(state.Revision, latestRevision, StringComparison.OrdinalIgnoreCase)
                ? ReferenceIndexCheckOutcome.UpToDate
                : ReferenceIndexCheckOutcome.RevisionChanged;
        }

        /// <summary>
        /// Whether the application may fetch this by itself, given that the
        /// user has asked it to keep the translations current.
        ///
        /// A change of language pair is never fetched unattended. It replaces
        /// translations that were working with ones for another language, and
        /// that has already happened once by accident: it is a question put to
        /// the user, and a question needs somebody at the keyboard.
        /// </summary>
        public static bool MayInstall(ReferenceIndexCheckOutcome outcome)
        {
            switch (outcome)
            {
                case ReferenceIndexCheckOutcome.Missing:
                case ReferenceIndexCheckOutcome.RulesChanged:
                case ReferenceIndexCheckOutcome.UnknownRevision:
                case ReferenceIndexCheckOutcome.RevisionChanged:
                    return true;

                default:
                    return false;
            }
        }

        /// <summary>
        /// Whether the outcome is worth a line on screen. Everything is, except
        /// a question that never reached GitHub - the user did nothing to
        /// prompt this, and an error about it would arrive out of nowhere.
        /// </summary>
        public static bool IsWorthSaying(ReferenceIndexCheckOutcome outcome)
        {
            return outcome != ReferenceIndexCheckOutcome.Unknown;
        }
    }
}
