using System;
using System.Windows;

namespace FFXIVTataruHelper.Services.UI
{
    /// <summary>
    /// Keeps the copy on screen through the gap between one line and the next.
    ///
    /// The game does not reuse its dialogue window: it tears the old one down
    /// and builds a new one for every line. For a frame or two in between there
    /// is nothing to be found, and a copy that believed it would blink out and
    /// back on every single line of a conversation.
    ///
    /// So a rectangle that has just been seen is held for a moment longer. The
    /// wait is short enough that a conversation genuinely ending still clears
    /// the copy within a blink, and long enough to cover the changeover.
    /// </summary>
    internal sealed class DialogueOverlayHold
    {
        public static readonly TimeSpan Grace = TimeSpan.FromMilliseconds(250);

        private Rect _last = Rect.Empty;
        private DateTime _lastSeenUtc = DateTime.MinValue;
        private bool _has;

        /// <summary>
        /// What to draw now, given what was found this moment. False when the
        /// copy should be off screen.
        /// </summary>
        public bool Decide(bool found, Rect rect, DateTime nowUtc, out Rect drawn)
        {
            if (found)
            {
                _last = rect;
                _lastSeenUtc = nowUtc;
                _has = true;
                drawn = rect;
                return true;
            }

            if (_has && nowUtc - _lastSeenUtc < Grace)
            {
                drawn = _last;
                return true;
            }

            _has = false;
            drawn = Rect.Empty;
            return false;
        }

        /// <summary>
        /// Forgets what was held. Used when the line itself goes away, where
        /// waiting out the gap would mean holding a box over a finished
        /// conversation.
        /// </summary>
        public void Clear()
        {
            _has = false;
            _last = Rect.Empty;
            _lastSeenUtc = DateTime.MinValue;
        }
    }
}
