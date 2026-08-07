using System;
using System.Threading;
using System.Threading.Tasks;

using Translation.Reference;

namespace FFXIVTataruHelper.Services.Update
{
    /// <summary>What the application currently holds of the hand-made translation.</summary>
    public readonly struct ReferenceIndexState
    {
        public ReferenceIndexState(bool isInstalled, string sourceLanguage, string language, string revision,
            int lines, int rulesVersion)
        {
            IsInstalled = isInstalled;
            RulesVersion = rulesVersion;
            SourceLanguage = sourceLanguage ?? string.Empty;
            Language = language ?? string.Empty;
            Revision = revision ?? string.Empty;
            Lines = lines;
        }

        public bool IsInstalled { get; }

        /// <summary>
        /// The language the lines are keyed on. Worth showing: an index keyed
        /// on a language the game is not being played in matches nothing, and
        /// nothing else on screen would explain why.
        /// </summary>
        public string SourceLanguage { get; }

        public string Language { get; }

        /// <summary>
        /// Empty for the index the application shipped with: it was built from a
        /// folder, and nothing recorded which commit that folder was at.
        /// </summary>
        public string Revision { get; }

        public int Lines { get; }

        /// <summary>
        /// The parsing rules it was built by. An index built by older ones is
        /// out of date even at the current revision, and saying "already up to
        /// date" to somebody in that position leaves them with a known fault.
        /// </summary>
        public int RulesVersion { get; }
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

        /// <summary>
        /// The pair an update would build for, asked before starting one so the
        /// user can be told it is not the pair they already have.
        /// </summary>
        (string GameLanguage, string ReadingLanguage) ResolveLanguages(string gameLanguage, string readingLanguage);

        /// <summary>
        /// What commit the translation project is at, or empty when it could
        /// not be asked.
        ///
        /// Separate from <see cref="UpdateAsync"/>, which asks the same
        /// question and then acts on it. This is for asking alone: it is a few
        /// kilobytes, so it can happen by itself once a day, while fetching
        /// the answer is a gigabyte and cannot.
        /// </summary>
        Task<string> GetLatestRevisionAsync(CancellationToken cancellationToken);

        /// <param name="gameLanguage">What the game is played in, so lines are keyed on it.</param>
        /// <param name="readingLanguage">What the user wants to read.</param>
        Task<ReferenceUpdateResult> UpdateAsync(
            string gameLanguage,
            string readingLanguage,
            IProgress<ReferenceUpdateProgress> progress,
            CancellationToken cancellationToken);
    }
}
