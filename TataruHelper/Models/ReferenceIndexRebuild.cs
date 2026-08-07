using System;

using FFXIVTataruHelper.Services.Update;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// Whether pressing the update button would replace the translations with
    /// ones for a different pair of languages, and how to say so.
    ///
    /// It would, and it did: the game was restarted in English while the
    /// application still believed it German, and the button fetched a German
    /// index over the top of the English one that had been working. Hundreds of
    /// megabytes, several minutes, and nothing on the way through said what was
    /// about to happen.
    /// </summary>
    public static class ReferenceIndexRebuild
    {
        /// <summary>
        /// Whether the pair about to be built is not the pair already
        /// installed. Nothing installed is nothing to lose, so nothing to ask.
        /// </summary>
        public static bool ChangesLanguages(
            ReferenceIndexState state, string gameLanguage, string readingLanguage)
        {
            if (!state.IsInstalled)
            {
                return false;
            }

            return !Same(state.SourceLanguage, gameLanguage) || !Same(state.Language, readingLanguage);
        }

        /// <summary>
        /// The question, with both pairs in it: which one is there now and
        /// which one it would become.
        /// </summary>
        public static string Question(
            ReferenceIndexState state, string gameLanguage, string readingLanguage,
            Func<string, string> localize)
        {
            return string.Format(
                localize("ReferenceIndexSwitchLanguages"),
                state.SourceLanguage + " → " + state.Language,
                gameLanguage + " → " + readingLanguage);
        }

        private static bool Same(string left, string right)
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }
}
