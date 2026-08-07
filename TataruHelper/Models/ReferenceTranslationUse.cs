using System;
using System.Collections.Generic;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// Whether the hand-made translations have anything to offer at all.
    ///
    /// They are a lookup from the language the game is played in to the
    /// language the user reads. Set both to the same language and there is
    /// nothing to look up: the line on screen is already the line wanted. The
    /// engine layer knows this and quietly answers nothing, which leaves a
    /// switch, a status line and an Update button for a gigabyte that would
    /// never be read.
    /// </summary>
    public static class ReferenceTranslationUse
    {
        /// <param name="gameLanguage">What the client draws its dialogue in.</param>
        /// <param name="readingLanguages">What each chat window translates into.</param>
        public static bool AnythingToLookUp(string gameLanguage, IEnumerable<string> readingLanguages)
        {
            // Not knowing is not the same as knowing there is nothing. The
            // game's configuration may be unreadable, or the game may never
            // have been run here; taking the translations off the page over
            // that would be hiding a working feature on a guess.
            if (string.IsNullOrEmpty(gameLanguage))
            {
                return true;
            }

            if (readingLanguages == null)
            {
                return true;
            }

            var any = false;

            foreach (var reading in readingLanguages)
            {
                any = true;

                // A window whose language has not been settled yet says
                // nothing either way, and the saved settings arrive late.
                if (string.IsNullOrEmpty(reading))
                {
                    return true;
                }

                if (!string.Equals(reading, gameLanguage, StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            // No windows at all is the moment before the saved ones are read
            // in, not a considered choice to read in the game's own language.
            return !any;
        }
    }
}
