using System;
using System.Drawing;

namespace FFXIVTataruHelper.EventArguments
{
    public class RectangleDValueChangeEventArgs : TatruEventArgs
    {
        public RectangleD OldValue { get; internal set; }

        public RectangleD NewValue { get; internal set; }

        internal RectangleDValueChangeEventArgs(Object sender) : base(sender) { }
    }
}
