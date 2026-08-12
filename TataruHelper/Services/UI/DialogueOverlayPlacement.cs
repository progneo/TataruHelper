using System.Windows;

using FFXIVTataruHelper.Services.GameMemory;

namespace FFXIVTataruHelper.Services.UI
{
    /// <summary>
    /// Decides whether a copy of the game's dialogue box should be on screen at
    /// this moment, and where.
    ///
    /// Kept apart from the window that does the drawing so the deciding can be
    /// checked without one: the answers depend on the game being in front, on a
    /// line being shown, and on that line having been translated, and each of
    /// those has been got wrong before in code that could only be watched.
    /// </summary>
    internal static class DialogueOverlayPlacement
    {
        /// <summary>
        /// The smallest box worth covering. A window being built or torn down
        /// reports a few pixels for a frame or two, and a copy that flashed at
        /// that size would read as a glitch rather than a translation.
        /// </summary>
        private const double SmallestUsefulSide = 40;

        public static bool TryPlace(
            bool enabled,
            bool gameInForeground,
            AddonBounds bounds,
            GameWindowProjection projection,
            string translatedText,
            out Rect rect)
        {
            rect = Rect.Empty;

            if (!enabled || !gameInForeground || string.IsNullOrWhiteSpace(translatedText))
            {
                return false;
            }

            if (!projection.TryProject(bounds, out var projected))
            {
                return false;
            }

            // Measured against the projected box rather than the raw one: what
            // matters is how big it comes out on the desktop, and on a scaled
            // display those are not the same number.
            if (projected.Width < SmallestUsefulSide || projected.Height < SmallestUsefulSide)
            {
                return false;
            }

            rect = projected;
            return true;
        }
    }
}
