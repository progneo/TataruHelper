using System;
using static FFXIVTataruHelper.WinUtils.MouseHooker;

namespace FFXIVTataruHelper.EventArguments
{
    public class LowLevelMouseEventArgs : TatruEventArgs
    {
        public MouseMessages MouseMessages { get; internal set; }
        public MSLLHOOKSTRUCT MouseEventFlags { get; internal set; }

        internal LowLevelMouseEventArgs(Object sender) : base(sender) { }
    }
}
