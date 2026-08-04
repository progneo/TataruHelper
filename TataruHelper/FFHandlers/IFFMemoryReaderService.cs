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

        /// <summary>
        /// Whether the game process is attached right now.
        ///
        /// FFWindowStateChanged only fires on transitions, and the reader starts
        /// before the settings window exists, so a UI created afterwards has no way
        /// to learn the state from the event alone.
        /// </summary>
        bool IsGameRunning { get; }

        /// <summary>Process name and PID of the attached game, empty when detached.</summary>
        string GameProcessDescription { get; }

        /// <summary>
        /// Reads dialogue from the game's UI as it appears rather than waiting for
        /// the chat log. Off falls back to chat-log-only behaviour.
        /// </summary>
        bool IsRealtimeTranslationEnabled { get; set; }

        void Start();

        void Stop();

        Task StopAsync(TimeSpan timeout);

        void AddExclusionWindowHandler(IntPtr handler);
    }
}