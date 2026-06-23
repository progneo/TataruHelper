using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class StringValueChangeEventArgs : TatruEventArgs
    {
        public string OldString { get; internal set; }

        public string NewString { get; internal set; }

        internal StringValueChangeEventArgs(Object sender) : base(sender) { }
    }
}
