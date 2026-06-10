using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class TimeSpanChangeEventArgs : TatruEventArgs
    {
        public TimeSpan OldValue { get; internal set; }

        public TimeSpan NewValue { get; internal set; }

        internal TimeSpanChangeEventArgs(Object sender) : base(sender) { }
    }
}
