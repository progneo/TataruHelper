using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class BooleanChangeEventArgs : TatruEventArgs
    {
        public bool OldValue { get; internal set; }

        public bool NewValue { get; internal set; }

        internal BooleanChangeEventArgs(Object sender) : base(sender) { }
    }
}
