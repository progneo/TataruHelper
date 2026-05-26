using System;
using System.Threading;
using System.Windows;

using FFXIVTataruHelper.Services.Logging;

namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public sealed class WpfClipboardService : IClipboardService
    {
        private const int MaxAttempts = 5;
        private const int RetryDelayMs = 40;

        private readonly IAppLogger _logger;

        public WpfClipboardService(IAppLogger logger)
        {
            _logger = logger;
        }

        public bool TryGetText(out string text)
        {
            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    text = Clipboard.ContainsText() ? Clipboard.GetText() ?? string.Empty : string.Empty;
                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxAttempts - 1)
                    {
                        _logger?.WriteLog("[CLIPBOARD_GET] " + ex.Message);
                        text = string.Empty;
                        return false;
                    }

                    Thread.Sleep(RetryDelayMs);
                }
            }

            text = string.Empty;
            return false;
        }

        public bool TrySetText(string text)
        {
            if (text == null)
            {
                text = string.Empty;
            }

            for (var attempt = 0; attempt < MaxAttempts; attempt++)
            {
                try
                {
                    Clipboard.SetText(text);
                    return true;
                }
                catch (Exception ex)
                {
                    if (attempt == MaxAttempts - 1)
                    {
                        _logger?.WriteLog("[CLIPBOARD_SET] " + ex.Message);
                        return false;
                    }

                    Thread.Sleep(RetryDelayMs);
                }
            }

            return false;
        }
    }
}