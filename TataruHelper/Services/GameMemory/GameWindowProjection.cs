using System.Windows;

namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// Turns a place inside the game's picture into a place on the desktop.
    ///
    /// Two things stand between them. The game's windows are placed against the
    /// top-left of its client area, which is not the top-left of the screen
    /// unless the game is filling it - so the corner is added. And the game
    /// counts in real pixels while WPF counts in units that are pixels only at
    /// 100% - so the result is divided by whatever the display is scaled to. A
    /// dialogue box drawn without that division lands too far down and to the
    /// right on every screen a reader is likely to be using.
    /// </summary>
    internal readonly struct GameWindowProjection
    {
        public static GameWindowProjection None => default;

        /// <param name="clientLeft">Left edge of the game's client area on the desktop, in pixels.</param>
        /// <param name="clientTop">Top edge of the same, in pixels.</param>
        /// <param name="dpiScale">1.0 at 100%, 1.5 at 150%, and so on.</param>
        public GameWindowProjection(int clientLeft, int clientTop, double dpiScale)
        {
            _clientLeft = clientLeft;
            _clientTop = clientTop;
            _dpiScale = dpiScale;
        }

        private readonly int _clientLeft;
        private readonly int _clientTop;
        private readonly double _dpiScale;

        /// <summary>
        /// False before the game's window has been found, and whenever the
        /// display says it is scaled by nothing - a figure nothing can be
        /// divided by, and one that means the answer was never read.
        /// </summary>
        public bool IsUsable => _dpiScale > 0;

        public bool TryProject(AddonBounds bounds, out Rect rect)
        {
            rect = Rect.Empty;

            if (!IsUsable || !bounds.IsKnown)
            {
                return false;
            }

            rect = new Rect(
                (_clientLeft + bounds.X) / _dpiScale,
                (_clientTop + bounds.Y) / _dpiScale,
                bounds.Width / _dpiScale,
                bounds.Height / _dpiScale);

            return true;
        }
    }
}
