using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.TataruComponentModel;
using System;
using System.Windows;

namespace FFXIVTataruHelper.FFHandlers
{
    public interface IFFMemoryReaderService : IDisposable, INotifyPropertyChangedAsync
    {
        event AsyncEventHandler<WindowStateChangeEventArgs> FFWindowStateChanged;

        event AsyncEventHandler<ChatMessageArrivedEventArgs> FFChatMessageArrived;

        WindowState FFWindowState { get; }

        bool UseDirectReading { get; set; }

        void Start();

        void Stop();

        void AddExclusionWindowHandler(IntPtr handler);
    }
}
