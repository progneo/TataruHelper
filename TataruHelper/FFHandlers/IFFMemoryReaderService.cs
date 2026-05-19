using System;
using System.Threading.Tasks;
using System.Windows;

using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.TataruComponentModel;

namespace FFXIVTataruHelper.FFHandlers
{
    public interface IFFMemoryReaderService : IDisposable, INotifyPropertyChangedAsync
    {
        event AsyncEventHandler<WindowStateChangeEventArgs> FFWindowStateChanged;

        event AsyncEventHandler<ChatMessageArrivedEventArgs> FFChatMessageArrived;

        WindowState FFWindowState { get; }

        bool IsGameWindowForeground { get; }

        void Start();

        void Stop();

        Task StopAsync(TimeSpan timeout);

        void AddExclusionWindowHandler(IntPtr handler);
    }
}