using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class WindowStateChangeEventArgs : TatruEventArgs
    {
        public System.Windows.WindowState OldWindowState { get; internal set; }

        public System.Windows.WindowState NewWindowState { get; internal set; }

        public string Text { get; internal set; }

        public bool IsRunningOld { get; internal set; }

        public bool IsRunningNew { get; internal set; }

        internal WindowStateChangeEventArgs(Object sender) : base(sender) { }
    }
}
