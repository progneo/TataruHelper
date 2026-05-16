using System;
using System.Threading.Tasks;
using System.Windows;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class WpfUiDispatcher : IUiDispatcher
    {
        private Window _window;

        public bool IsInitialized => _window != null;

        public Window CurrentWindow => _window;

        public void SetWindow(Window window)
        {
            _window = window;
        }

        public void Invoke(Action action)
        {
            EnsureWindow().UIThread(action);
        }

        public Task InvokeAsync(Action action)
        {
            return EnsureWindow().UIThreadAsync(action);
        }

        private Window EnsureWindow()
        {
            if (_window == null)
            {
                throw new InvalidOperationException("UI dispatcher window is not initialized.");
            }

            return _window;
        }
    }
}
