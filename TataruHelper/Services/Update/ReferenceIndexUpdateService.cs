using System;
using System.Threading;
using System.Threading.Tasks;

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
                return new ReferenceIndexState(false, string.Empty, string.Empty, 0);
            }

            return new ReferenceIndexState(
                _webTranslator.ReferenceIndexLines > 0,
                _webTranslator.ReferenceIndexLanguage,
                _webTranslator.ReferenceIndexRevision,
                _webTranslator.ReferenceIndexLines);
        }

        public async Task<ReferenceUpdateResult> UpdateAsync(
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
                return await updater.UpdateAsync(
                    _webTranslator.ReferenceIndexPath,
                    _webTranslator.ReferenceIndexTargetLanguage,
                    _webTranslator.ReferenceIndexRevision,
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
