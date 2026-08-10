using System;
using System.Collections.Generic;

namespace FFXIVTataruHelper.FFHandlers
{
    /// <summary>
    /// Everything about the game side of a session that a bug report needs, in
    /// one read.
    ///
    /// Gathered because the answer to "translation does not work" is almost
    /// always one of a handful of facts nobody can see from the outside: the
    /// game was never attached, the character was never resolved, or lines are
    /// being read for one dialogue channel and not the other. Guessing between
    /// them costs a round trip to the person reporting it, and each round trip
    /// costs a day.
    /// </summary>
    public sealed class GameReadingDiagnostics
    {
        public static readonly GameReadingDiagnostics Unavailable = new GameReadingDiagnostics(
            false, false, string.Empty, string.Empty, false, false, 0, Array.Empty<string>());

        public GameReadingDiagnostics(
            bool gameAttached,
            bool everAttached,
            string processDescription,
            string gameLanguage,
            bool playerResolved,
            bool realtimeEnabled,
            int linesReadLive,
            IReadOnlyList<string> codesReadLive)
        {
            GameAttached = gameAttached;
            EverAttached = everAttached || gameAttached;
            ProcessDescription = processDescription ?? string.Empty;
            GameLanguage = gameLanguage ?? string.Empty;
            PlayerResolved = playerResolved;
            RealtimeEnabled = realtimeEnabled;
            LinesReadLive = linesReadLive;
            CodesReadLive = codesReadLive ?? Array.Empty<string>();
        }

        public bool GameAttached { get; }

        /// <summary>
        /// Whether a game was attached at any point since startup. What follows
        /// in a report describes that attachment, and while it is false there is
        /// nothing behind any of it.
        /// </summary>
        public bool EverAttached { get; }

        public string ProcessDescription { get; }

        public string GameLanguage { get; }

        /// <summary>
        /// Whether the character has been read out of the game's memory. False
        /// while attached means the signature scan is not working on this client,
        /// which is the difference between a settings problem and a broken read.
        /// </summary>
        public bool PlayerResolved { get; }

        public bool RealtimeEnabled { get; }

        public int LinesReadLive { get; }

        public IReadOnlyList<string> CodesReadLive { get; }
    }
}
