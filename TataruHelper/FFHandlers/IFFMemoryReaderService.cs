using System;
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

        bool UseDirectReading { get; set; }

        void Start();

        void Stop();

        void AddExclusionWindowHandler(IntPtr handler);
    }
}