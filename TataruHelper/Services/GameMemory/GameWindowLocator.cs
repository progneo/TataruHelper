using System;
using System.Runtime.InteropServices;

namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// Asks Windows where the game's picture begins and how the display is
    /// scaled, so a copy of one of its windows can be put on top of it.
    ///
    /// Everything here is a call out to the operating system, which is why it
    /// is kept apart from the arithmetic it feeds: that part is worked out in
    /// <see cref="GameWindowProjection"/> and can be checked without a game
    /// running, while this part can only be watched.
    /// </summary>
    internal static class GameWindowLocator
    {
        private const int DefaultDpi = 96;

        public static bool TryLocate(IntPtr gameWindow, out GameWindowProjection projection)
        {
            projection = GameWindowProjection.None;

            if (gameWindow == IntPtr.Zero)
            {
                return false;
            }

            // The corner of the client area, which is where the game counts
            // from. Taken by asking Windows to translate the client area's own
            // origin, so a window, a borderless window and a full screen are
            // all answered the same way and none of them is a special case.
            var origin = default(NativePoint);
            if (!ClientToScreen(gameWindow, ref origin))
            {
                return false;
            }

            projection = new GameWindowProjection(origin.X, origin.Y, ReadScale(gameWindow));
            return projection.IsUsable;
        }

        /// <summary>
        /// How much the display the game is on magnifies things.
        ///
        /// Read from the game's window rather than ours: the two can be on
        /// different monitors with different settings, and it is the game's
        /// picture that the copy has to line up with.
        /// </summary>
        private static double ReadScale(IntPtr gameWindow)
        {
            try
            {
                var dpi = GetDpiForWindow(gameWindow);
                return dpi > 0 ? dpi / (double)DefaultDpi : 1.0;
            }
            catch (EntryPointNotFoundException)
            {
                // Older than Windows 10 1607. Nothing to do but assume the
                // display is not scaled, which is what it was before the call
                // existed.
                return 1.0;
            }
            catch (DllNotFoundException)
            {
                return 1.0;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct NativePoint
        {
            public int X;
            public int Y;
        }

        [DllImport("user32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ClientToScreen(IntPtr hWnd, ref NativePoint point);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);
    }
}
