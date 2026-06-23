using System;
using System.Drawing;

namespace FFXIVTataruHelper.EventArguments
{
    public class PointDValueChangeEventArgs : TatruEventArgs
    {
        public PointD OldValue { get; internal set; }

        public PointD NewValue { get; internal set; }

        internal PointDValueChangeEventArgs(Object sender) : base(sender) { }
    }
}
