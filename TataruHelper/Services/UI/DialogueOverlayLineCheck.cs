using System;

using FFXIVTataruHelper.Services.GameMemory;

namespace FFXIVTataruHelper.Services.UI
{
    /// <summary>
    /// Whether the line a copy is showing is still the line the game is showing.
    ///
    /// A translation is on its way for a little while, and in that while the
    /// game moves on: another box, another line. The line read off the screen
    /// is the only judge of which conversation the copy is in, so that is what
    /// the copy is matched on - and reduced the same way the live reader
    /// reduces its own lines, so both render the same utterance the same.
    ///
    /// Kept apart from the window that does the showing, the way the placement
    /// and the hold are.
    /// </summary>
    internal static class DialogueOverlayLineCheck
    {
        /// <summary>
        /// Reduces a line to what is said in it, so the line read off the
        /// screen and the line put through the translator compare equal. Who
        /// said it, how it is cased and how its spaces are set do not make one
        /// line another.
        /// </summary>
        public static string KeyOf(string line)
        {
            return SharlayanGameMemoryGateway.BuildDuplicateKey(line);
        }

        /// <summary>
        /// Whether the copy keyed by <paramref name="shownKey"/> belongs on the
        /// line the game is drawing right now.
        ///
        /// The two questions that cannot be answered are answered in the copy's
        /// favour: with nothing to check on there is nothing to take away, and
        /// a line the screen cannot be read off is not evidence the copy is
        /// stale, only that nobody can say.
        /// </summary>
        public static bool IsCurrent(string shownKey, string gameLine)
        {
            if (string.IsNullOrEmpty(shownKey))
            {
                return true;
            }

            var gameKey = KeyOf(gameLine);
            return gameKey.Length == 0 ||
                   string.Equals(gameKey, shownKey, StringComparison.Ordinal);
        }
    }
}