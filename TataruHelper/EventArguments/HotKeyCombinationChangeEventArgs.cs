using FFXIVTataruHelper.WinUtils;
using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class HotKeyCombinationChangeEventArgs : TatruEventArgs
    {
        public HotKeyCombination OldHotKeyCombination;

        public HotKeyCombination NewHotKeyCombination;

        internal HotKeyCombinationChangeEventArgs(Object sender) : base(sender) { }
    }
}
