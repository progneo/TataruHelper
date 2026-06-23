using System;
using System.Collections.Generic;

namespace FFXIVTataruHelper.EventArguments
{
    public class ChatMsgTypeChangeEventArgs : TatruEventArgs
    {
        public Dictionary<string, ChatMsgType> ChatCodes { get; internal set; }

        internal ChatMsgTypeChangeEventArgs(Object sender) : base(sender) { }
    }
}
