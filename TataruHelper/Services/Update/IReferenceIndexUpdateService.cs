using System;
using System.Threading;
using System.Threading.Tasks;

using Translation.Reference;

namespace FFXIVTataruHelper.Services.Update
{
    /// <summary>What the application currently holds of the hand-made translation.</summary>
    public readonly struct ReferenceIndexState
    {
        public ReferenceIndexState(bool isInstalled, string language, string revision, int lines)
        {
            IsInstalled = isInstalled;
            Language = language ?? string.Empty;
            Revision = revision ?? string.Empty;
            Lines = lines;
        }

        public bool IsInstalled { get; }

        public string Language { get; }

        /// <summary>
        /// Empty for the index the application shipped with: it was built from a
        /// folder, and nothing recorded which commit that folder was at.
        /// </summary>
        public string Revision { get; }

        public int Lines { get; }
    }

    /// <summary>
    /// Rebuilds the index of hand-made translations from the translation
    /// project, and puts the result to work without a restart.
    /// </summary>
    public interface IReferenceIndexUpdateService
    {
        /// <summary>Whether there is an index to update at all.</summary>
        bool IsSupported { get; }

        ReferenceIndexState ReadState();

        Task<ReferenceUpdateResult> UpdateAsync(
            IProgress<ReferenceUpdateProgress> progress,
            CancellationToken cancellationToken);
    }
}
