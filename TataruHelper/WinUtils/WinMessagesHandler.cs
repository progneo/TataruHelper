using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Utils;
using System;
using System.Windows;
using System.Windows.Interop;

namespace FFXIVTataruHelper.WinUtils
{
    public sealed class WinMessagesHandler
    {
        #region **Events.

        public event AsyncEventHandler<BooleanChangeEventArgs> ShowFirstInstance
        {
            add { this._ShowFirstInstance.Register(value); }
            remove { this._ShowFirstInstance.Unregister(value); }
        }
        private AsyncEvent<BooleanChangeEventArgs> _ShowFirstInstance;

        #endregion

        #region **Properties.

        private HwndSource _HwndSource;
        private HwndSourceHook _Hook;
        private readonly IAppLogger _logger;

        #endregion

        public WinMessagesHandler(IAppLogger logger)
        {
            _logger = logger;
            _ShowFirstInstance = new AsyncEvent<BooleanChangeEventArgs>(EventErrorHandler, "ShowFirstInstance");
        }

        public void Attach(Window window)
        {
            if (_HwndSource != null)
                return;

            _HwndSource = (HwndSource)HwndSource.FromVisual(window);

            _Hook = new HwndSourceHook(WndProc);

            _HwndSource.AddHook(_Hook);
        }

        #region **Listen to Windows messages.
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == TataruSingleInstance.WM_SHOWFIRSTINSTANCE)
            {
                var ea = new BooleanChangeEventArgs(this)
                {
                    OldValue = false,
                    NewValue = true
                };

                _ShowFirstInstance.InvokeAsync(ea).Forget();
            }

            return IntPtr.Zero;
        }
        #endregion

        private void EventErrorHandler(string evname, Exception ex)
        {
            string text = evname + Environment.NewLine + Convert.ToString(ex);
            _logger.WriteLog(text);
        }

    }
}
