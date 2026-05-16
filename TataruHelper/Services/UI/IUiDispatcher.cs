using System;
using System.Threading.Tasks;
using System.Windows;

namespace FFXIVTataruHelper.Services.UI
{
    public interface IUiDispatcher
    {
        bool IsInitialized { get; }

        Window CurrentWindow { get; }

        void SetWindow(Window window);

        void Invoke(Action action);

        Task InvokeAsync(Action action);
    }
}
