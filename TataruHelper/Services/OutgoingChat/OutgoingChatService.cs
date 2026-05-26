using System;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper.Services.Logging;

using Translation.Core;
using Translation.OutgoingChat;

namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public sealed class OutgoingChatService : IOutgoingChatService
    {
        private readonly WebTranslator _webTranslator;
        private readonly OutgoingMessageComposer _composer;
        private readonly IClipboardService _clipboardService;
        private readonly ClipboardRestorer _clipboardRestorer;
        private readonly IAppLogger _logger;

        public OutgoingChatService(
            WebTranslator webTranslator,
            OutgoingMessageComposer composer,
            IClipboardService clipboardService,
            ClipboardRestorer clipboardRestorer,
            IAppLogger logger)
        {
            _webTranslator = webTranslator ?? throw new ArgumentNullException(nameof(webTranslator));
            _composer = composer ?? throw new ArgumentNullException(nameof(composer));
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _clipboardRestorer = clipboardRestorer ?? throw new ArgumentNullException(nameof(clipboardRestorer));
            _logger = logger;
        }

        public async Task<OutgoingChatResult> TranslateAndCopyAsync(OutgoingChatRequest request,
            CancellationToken cancellationToken)
        {
            if (request == null)
            {
                return OutgoingChatResult.Failure(OutgoingChatResultKind.EmptyInput, "Request is null.");
            }

            var input = (request.Text ?? string.Empty).Trim();
            if (input.Length == 0)
            {
                return OutgoingChatResult.Failure(OutgoingChatResultKind.EmptyInput, "Message is empty.");
            }

            string clipboardPayload;
            TranslationResult translation;
            try
            {
                translation = await _webTranslator
                    .TranslateAsync(input, request.Engine, request.FromLanguage, request.ToLanguage, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.WriteLog("[OUTGOING_CHAT_TRANSLATE] " + ex);
                return OutgoingChatResult.Failure(OutgoingChatResultKind.TranslationFailed, ex.Message);
            }

            if (!translation.IsSuccess || string.IsNullOrEmpty(translation.Text))
            {
                return OutgoingChatResult.Failure(OutgoingChatResultKind.TranslationFailed,
                    translation.FailureReason ?? "Translation failed.", translation);
            }

            try
            {
                var options = new OutgoingMessageComposeOptions
                {
                    PrependChannelCommand = request.PrependChannelCommand,
                    AppendOriginalInParentheses = request.AppendOriginalInParentheses
                };

                clipboardPayload = _composer.Compose(translation.Text, input, request.Channel, request.TellTarget,
                    options);
            }
            catch (ArgumentException ex)
            {
                return OutgoingChatResult.Failure(OutgoingChatResultKind.InvalidTellTarget, ex.Message, translation);
            }

            string previousClipboard = null;
            if (request.RestoreClipboardAfterDelay)
            {
                _clipboardService.TryGetText(out previousClipboard);
            }

            if (!_clipboardService.TrySetText(clipboardPayload))
            {
                return OutgoingChatResult.Failure(OutgoingChatResultKind.ClipboardFailed,
                    "Failed to set clipboard text.", translation);
            }

            if (request.RestoreClipboardAfterDelay && request.ClipboardRestoreDelaySeconds > 0)
            {
                _clipboardRestorer.ScheduleRestore(
                    previousClipboard ?? string.Empty,
                    clipboardPayload,
                    TimeSpan.FromSeconds(request.ClipboardRestoreDelaySeconds));
            }
            else
            {
                _clipboardRestorer.Cancel();
            }

            return OutgoingChatResult.Success(clipboardPayload, translation);
        }
    }
}