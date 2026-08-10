using System;
using System.Collections.Generic;

namespace FFXIVTataruHelper.Services.GameMemory
{
    /// <summary>
    /// What the gateway has managed to read off the game's screen since it
    /// attached.
    ///
    /// Exists to be reported, not acted on. When somebody says translation does
    /// not work, this is the fact that separates the three ways it can fail:
    /// nothing read at all means the memory reading never got going, and a
    /// dialogue code present while the other is missing means one addon is
    /// readable on their client and the other is not.
    /// </summary>
    public sealed class LiveReadingStats
    {
        public static readonly LiveReadingStats None = new LiveReadingStats(0, Array.Empty<string>());

        public LiveReadingStats(int lines, IReadOnlyList<string> codes)
        {
            Lines = lines;
            Codes = codes ?? Array.Empty<string>();
        }

        /// <summary>Lines read off the screen and reported.</summary>
        public int Lines { get; }

        /// <summary>Chat codes at least one of those lines arrived under.</summary>
        public IReadOnlyList<string> Codes { get; }
    }
}
