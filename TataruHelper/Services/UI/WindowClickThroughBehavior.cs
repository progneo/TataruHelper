using System;
using System.Windows;
using System.Windows.Interop;

using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.WinUtils;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class WindowClickThroughBehavior
    {
        private readonly Window _window;
        private readonly IAppLogger _logger;
        private bool _isClickThrough;

        public WindowClickThroughBehavior(Window window, IAppLogger logger)
        {
            _window = window;
            _logger = logger;
        }

        public void MakeClickThrough()
        {
            try
            {
                if (!_isClickThrough)
                {
                    _window.UIThread(() =>
                    {
                        var hwnd = new WindowInteropHelper(_window).Handle;
                        var style = Win32Interfaces.GetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE);
                        Win32Interfaces.SetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE,
                            style | Win32Interfaces.WS_EX_LAYERED | Win32Interfaces.WS_EX_TRANSPARENT);
                        _isClickThrough = true;
                    });
                }
            }
            catch (Exception e)
            {
                _logger.WriteLog(Convert.ToString(e));
            }
        }

        public void MakeClickable()
        {
            try
            {
                if (_isClickThrough)
                {
                    _window.UIThread(() =>
                    {
                        var hwnd = new WindowInteropHelper(_window).Handle;
                        var style = Win32Interfaces.GetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE);
                        Win32Interfaces.SetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE,
                            style ^ Win32Interfaces.WS_EX_LAYERED ^ Win32Interfaces.WS_EX_TRANSPARENT);
                        _isClickThrough = false;
                    });
                }
            }
            catch (Exception e)
            {
                _logger.WriteLog(Convert.ToString(e));
            }
        }
    }
}