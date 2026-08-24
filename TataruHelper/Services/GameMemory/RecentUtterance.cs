using System;
using System.Collections.Generic;

namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// What has just been said, however it reached us.
    ///
    /// A line arrives by two roads: read off the screen, and again from the
    /// game's own chat log. Which arrives first is not fixed - in a duty the
    /// chat log wins by some forty milliseconds - so a guard that only asks
    /// "have we read this off the screen yet?" lets the pair through whenever
    /// the log gets there first, which is every line of a duty.
    ///
    /// So both roads report here, and both are asked. Two things separate a
    /// second copy from a real repeat. Time: the same words a moment apart are
    /// one utterance arriving twice, the same words much later are somebody
    /// saying them again. And the name: copies differ by one of them having
    /// none, because the subtitle strip and the bubbles name nobody. Cid and
    /// Yda can both say "Understood." in the same breath and both deserve
    /// showing - they are named, and named differently.
    /// </summary>
    internal sealed class RecentUtterance
    {
        /// <summary>
        /// How far apart two arrivals can be and still be the same breath.
        /// Measured off a duty: the two roads were 0.04 to 1 second apart, and
        /// the same line genuinely said again came 33 seconds later.
        /// </summary>
        public static readonly TimeSpan SameBreath = TimeSpan.FromSeconds(2);

        /// <summary>
        /// How many are kept. A duty can put five lines on screen inside a
        /// second, and one slot only ever remembered the last of them.
        /// </summary>
        private const int Remembered = 16;

        private readonly Queue<Said> _said = new Queue<Said>();

        /// <summary>
        /// True when these words already reached us a moment ago by either
        /// road. Remembers them when they did not.
        /// </summary>
        public bool IsEcho(string words, string speaker, DateTime now)
        {
            if (Knows(words, speaker, now))
            {
                return true;
            }

            Note(words, speaker, now);
            return false;
        }

        /// <summary>
        /// Records words that went out without asking whether they were an
        /// echo - for the road that has already decided to show them.
        /// </summary>
        public void Note(string words, string speaker, DateTime now)
        {
            words = words ?? string.Empty;
            if (words.Length == 0)
            {
                return;
            }

            _said.Enqueue(new Said(words, speaker ?? string.Empty, now));

            while (_said.Count > Remembered)
            {
                _said.Dequeue();
            }
        }

        /// <summary>Forgets everything. For attaching to another game.</summary>
        public void Forget()
        {
            _said.Clear();
        }

        private bool Knows(string words, string speaker, DateTime now)
        {
            words = words ?? string.Empty;
            speaker = speaker ?? string.Empty;

            if (words.Length == 0)
            {
                return false;
            }

            foreach (var said in _said)
            {
                var age = now - said.At;
                if (age < TimeSpan.Zero || age >= SameBreath)
                {
                    continue;
                }

                if (!string.Equals(said.Words, words, StringComparison.Ordinal))
                {
                    continue;
                }

                // One of the two must be unnamed. Two names that differ are two
                // characters, however alike the words: swallowing the second is
                // how "Understood." from Cid eats "Understood." from Yda.
                if (speaker.Length == 0 ||
                    said.Speaker.Length == 0 ||
                    string.Equals(speaker, said.Speaker, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private readonly struct Said
        {
            public Said(string words, string speaker, DateTime at)
            {
                Words = words;
                Speaker = speaker;
                At = at;
            }

            public string Words { get; }
            public string Speaker { get; }
            public DateTime At { get; }
        }
    }
}
