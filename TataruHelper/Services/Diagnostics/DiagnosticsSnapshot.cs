using System;
using System.Collections.Generic;

using FFXIVTataruHelper.FFHandlers;

namespace FFXIVTataruHelper.Services.Diagnostics
{
    /// <summary>
    /// The facts a diagnostics report is written from, gathered so the writing
    /// of it can be tested without a game, a window, or a disk.
    /// </summary>
    public sealed class DiagnosticsSnapshot
    {
        public string AppVersion { get; set; } = string.Empty;

        /// <summary>Whether this copy was installed, or unpacked from the portable zip.</summary>
        public bool IsInstalled { get; set; }

        public string OperatingSystem { get; set; } = string.Empty;

        public bool IsElevated { get; set; }

        public string UiLanguage { get; set; } = string.Empty;

        public GameReadingDiagnostics Reading { get; set; } = GameReadingDiagnostics.Unavailable;

        public bool ReferenceTranslationEnabled { get; set; }

        /// <summary>How the XIV Rus Translation index describes itself, or why it cannot.</summary>
        public string ReferenceIndex { get; set; } = string.Empty;

        public IReadOnlyList<DiagnosticsWindow> Windows { get; set; } = Array.Empty<DiagnosticsWindow>();

        public string LogPath { get; set; } = string.Empty;
    }

    /// <summary>One chat window, as far as a report cares.</summary>
    public sealed class DiagnosticsWindow
    {
        public string Name { get; set; } = string.Empty;

        public string Engine { get; set; } = string.Empty;

        public string FromLanguage { get; set; } = string.Empty;

        public string ToLanguage { get; set; } = string.Empty;

        /// <summary>
        /// The ticked codes. Which of the dialogue codes are among them is the
        /// first thing to check when somebody sees no story dialogue.
        /// </summary>
        public IReadOnlyList<string> TickedCodes { get; set; } = Array.Empty<string>();
    }
}
