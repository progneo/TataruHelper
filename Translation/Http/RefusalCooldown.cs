using System;
using System.Threading;

namespace Translation.Http
{
    /// <summary>
    /// Remembers that a service asked to be left alone, and for how long.
    ///
    /// Without this, a refusal changes nothing about what happens next: the very
    /// next line knocks again, is refused again, and the rate the service
    /// objected to is exactly the rate it keeps receiving. Each of those lines
    /// also waits out a round trip before it can be handed to another engine.
    ///
    /// Lines are translated concurrently, so reads and writes go through
    /// Interlocked - a DateTime is wider than a word, and a torn one would put
    /// the end of the pause somewhere neither thread chose.
    /// </summary>
    internal sealed class RefusalCooldown
    {
        private readonly TimeSpan _duration;
        private long _untilTicksUtc;

        public RefusalCooldown(TimeSpan duration)
        {
            _duration = duration;
        }

        public void Record(DateTime utcNow)
        {
            Interlocked.Exchange(ref _untilTicksUtc, (utcNow + _duration).Ticks);
        }

        public bool IsActiveAt(DateTime utcNow, out DateTime untilUtc)
        {
            untilUtc = new DateTime(Interlocked.Read(ref _untilTicksUtc), DateTimeKind.Utc);
            return utcNow < untilUtc;
        }

        /// <summary>Lets a service that answers normally again end the pause early.</summary>
        public void Clear()
        {
            Interlocked.Exchange(ref _untilTicksUtc, 0);
        }
    }
}
