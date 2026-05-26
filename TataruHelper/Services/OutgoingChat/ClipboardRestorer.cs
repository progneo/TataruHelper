using System;
using System.Threading;

using FFXIVTataruHelper.Services.UI;

namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public sealed class ClipboardRestorer : IDisposable
    {
        private readonly IClipboardService _clipboardService;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly object _gate = new object();

        private Timer _timer;
        private string _expectedCurrent;
        private string _savedPrevious;

        public ClipboardRestorer(IClipboardService clipboardService, IUiDispatcher uiDispatcher)
        {
            _clipboardService = clipboardService ?? throw new ArgumentNullException(nameof(clipboardService));
            _uiDispatcher = uiDispatcher ?? throw new ArgumentNullException(nameof(uiDispatcher));
        }

        public void ScheduleRestore(string previousClipboard, string newClipboard, TimeSpan delay)
        {
            lock (_gate)
            {
                CancelLocked();

                _savedPrevious = previousClipboard ?? string.Empty;
                _expectedCurrent = newClipboard ?? string.Empty;

                _timer = new Timer(OnTimerTick, null, delay, Timeout.InfiniteTimeSpan);
            }
        }

        public void Cancel()
        {
            lock (_gate)
            {
                CancelLocked();
            }
        }

        public void Dispose()
        {
            Cancel();
        }

        private void CancelLocked()
        {
            _timer?.Dispose();
            _timer = null;
            _expectedCurrent = null;
            _savedPrevious = null;
        }

        private void OnTimerTick(object state)
        {
            string expected;
            string previous;

            lock (_gate)
            {
                if (_timer == null)
                {
                    return;
                }

                expected = _expectedCurrent;
                previous = _savedPrevious;
                CancelLocked();
            }

            if (expected == null)
            {
                return;
            }

            _uiDispatcher.InvokeAsync(() =>
            {
                if (!_clipboardService.TryGetText(out var current))
                {
                    return;
                }

                if (!string.Equals(current, expected, StringComparison.Ordinal))
                {
                    return;
                }

                _clipboardService.TrySetText(previous);
            });
        }
    }
}