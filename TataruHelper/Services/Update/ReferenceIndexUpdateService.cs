using System;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.Logging;

using Microsoft.Extensions.Logging;

using Translation;
using Translation.Reference;

namespace FFXIVTataruHelper.Services.Update
{
    /// <summary>
    /// Rebuilds the index of hand-made translations and hands it straight to the
    /// running translator.
    ///
    /// The translation project gains lines every week, and until now the only
    /// way to have them was a new release of the application, since the index is
    /// shipped inside it. Doing it here also means the user is not left with the
    /// application restarting itself over a file it downloaded.
    /// </summary>
    public sealed class ReferenceIndexUpdateService : IReferenceIndexUpdateService
    {
        private readonly WebTranslator _webTranslator;
        private readonly ILogger _logger;
        private readonly IAppLogger _appLogger;

        public ReferenceIndexUpdateService(WebTranslator webTranslator, ILogger logger, IAppLogger appLogger)
        {
            _webTranslator = webTranslator;
            _logger = logger;
            _appLogger = appLogger;
        }

        public bool IsSupported => !string.IsNullOrEmpty(_webTranslator?.ReferenceIndexPath);

        public ReferenceIndexState ReadState()
        {
            if (_webTranslator == null)
            {
                return new ReferenceIndexState(false, string.Empty, string.Empty, string.Empty, 0, 0);
            }

            return new ReferenceIndexState(
                _webTranslator.ReferenceIndexLines > 0,
                _webTranslator.ReferenceIndexSourceLanguage,
                _webTranslator.ReferenceIndexLanguage,
                _webTranslator.ReferenceIndexRevision,
                _webTranslator.ReferenceIndexLines,
                _webTranslator.ReferenceIndexRulesVersion);
        }

        /// <summary>
        /// The pair an update would build for.
        ///
        /// The game's configuration is read again here rather than trusted from
        /// startup. It is two kilobytes, and the alternative was found the hard
        /// way: the game was restarted in another language, nothing told the
        /// application, and a press of the button fetched an index for the
        /// language it used to be in - over the top of the one that worked.
        /// </summary>
        public (string GameLanguage, string ReadingLanguage) ResolveLanguages(
            string gameLanguage, string readingLanguage)
        {
            var detected = GameClientLanguage.Detect(_appLogger);
            if (detected.Length > 0)
            {
                _webTranslator.GameLanguage = detected;
            }

            // "auto" arrives here when a window is set to work the language out
            // for itself, and it is not a language.
            var game = ReferenceIndexUpdater.ResolveGameLanguage(
                gameLanguage?.Length > 0 ? gameLanguage : detected,
                _webTranslator.ReferenceIndexGameLanguage);

            var reading = readingLanguage?.Length > 0
                ? readingLanguage
                : _webTranslator.ReferenceIndexTargetLanguage;

            return (game, reading);
        }

        public Task<string> GetLatestRevisionAsync(CancellationToken cancellationToken)
        {
            return new ReferenceIndexUpdater(_logger).GetLatestRevisionAsync(cancellationToken);
        }

        public async Task<ReferenceUpdateResult> UpdateAsync(
            string gameLanguage,
            string readingLanguage,
            IProgress<ReferenceUpdateProgress> progress,
            CancellationToken cancellationToken)
        {
            if (!IsSupported)
            {
                return new ReferenceUpdateResult(ReferenceUpdateOutcome.Failed,
                    "There is no reference index to update.", 0);
            }

            var updater = new ReferenceIndexUpdater(_logger);
            var released = false;

            try
            {
                var (game, reading) = ResolveLanguages(gameLanguage, readingLanguage);

                // The revision is only worth comparing when the index would
                // otherwise be the same one: built for the same pair, and by
                // the same rules. Offered otherwise, it answers "already up to
                // date" and leaves the user with an index this build knows to
                // be wrong.
                var keepRevision = ReferenceIndexRebuild.CanKeepRevision(ReadState(), game, reading);

                return await updater.UpdateAsync(
                    _webTranslator.ReferenceIndexPath,
                    game,
                    reading,
                    keepRevision ? _webTranslator.ReferenceIndexRevision : string.Empty,
                    progress,
                    () =>
                    {
                        released = true;
                        _webTranslator.CloseReferenceIndex();
                    },
                    cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                // Reopened whether or not the swap worked. If it did, this is
                // the new index; if the move failed halfway, the old file is
                // still there and the user keeps what they had. Either way the
                // application must not be left holding a closed index.
                if (released)
                {
                    try
                    {
                        _webTranslator.ReopenReferenceIndex();
                    }
                    catch (Exception ex)
                    {
                        _appLogger?.WriteLog("Failed to reopen the reference index after an update.");
                        _appLogger?.WriteLog(ex);
                    }
                }
            }
        }
    }
}
