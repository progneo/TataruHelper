using System.Collections.Generic;
using System.Linq;

namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// Which of the speech bubbles on screen was just said.
    ///
    /// The bubble addon holds several at once - five, in a duty - and they sit
    /// there long after the character who said them has gone quiet. Asked for
    /// the longest of them, the reader answered with the same stale sentence
    /// for minutes: a passer-by's "I haven't the faintest what's going on, but
    /// you'd best keep moving!" outran every live bubble simply by being longer
    /// than them.
    ///
    /// Length says nothing about which one is being said. Appearing does: the
    /// bubble that was not there a moment ago is the one that has just been
    /// spoken, and when none of them is new, nobody has said anything new.
    /// </summary>
    internal sealed class SpeechBubbles
    {
        private HashSet<string> _onScreen = new HashSet<string>(System.StringComparer.Ordinal);

        /// <summary>
        /// The bubble to announce, or empty when none of those on screen is
        /// new. Remembers what is there either way.
        /// </summary>
        public string Pick(IReadOnlyList<string> bubbles)
        {
            var showing = bubbles ?? (IReadOnlyList<string>)System.Array.Empty<string>();

            var appeared = showing
                .Where(bubble => !string.IsNullOrEmpty(bubble) && !_onScreen.Contains(bubble))
                .ToArray();

            _onScreen = new HashSet<string>(
                showing.Where(bubble => !string.IsNullOrEmpty(bubble)),
                System.StringComparer.Ordinal);

            // The first look is not held back. It was, on the reasoning that
            // what is on screen at attach is nobody speaking to us - but the
            // addon is only read when it holds something, so the first look is
            // usually a bubble genuinely being said, and holding it back lost
            // the opening line of a duty every session. Announcing one stale
            // bubble once is the smaller fault; losing a real line is not.

            // Two at once is possible - two characters can speak over each
            // other - and then the longer is the better guess at which the
            // player is meant to be reading.
            return appeared.OrderByDescending(bubble => bubble.Length).FirstOrDefault() ?? string.Empty;
        }

        /// <summary>Forgets the screen. For attaching to another game.</summary>
        public void Forget()
        {
            _onScreen.Clear();
        }
    }
}
