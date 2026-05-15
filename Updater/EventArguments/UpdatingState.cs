using System;

namespace Updater.EventArguments
{
    public class UpdateStateChangedEventArgs : EventArgs
    {
        public UpdateStateChangedEventArgs(UpdateState state, string text = "", Exception error = null)
        {
            State = state;
            Text = text ?? string.Empty;
            Error = error;
        }

        public UpdateState State { get; private set; }

        public string Text { get; private set; }

        public Exception Error { get; private set; }
    }

    public enum UpdateState
    {
        Initializing,
        LookingForUpdates,
        NoUpdatesFound,
        Downloading,
        ReadyToRestart,
        Finished,
        Error
    }
}
