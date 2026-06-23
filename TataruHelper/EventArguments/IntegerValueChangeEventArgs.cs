using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class IntegerValueChangeEventArgs : TatruEventArgs
    {
        public int OldValue { get; internal set; }

        public int NewValue { get; internal set; }

        internal IntegerValueChangeEventArgs(Object sender) : base(sender) { }
    }
}
