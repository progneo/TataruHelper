using FFXIVTataruHelper.FFHandlers;
using System;

namespace FFXIVTataruHelper.EventArguments
{
    public class ChatMessageArrivedEventArgs : TatruEventArgs
    {
        public FFChatMsg ChatMessage { get; internal set; }

        internal ChatMessageArrivedEventArgs(Object sender) : base(sender) { }

        public ChatMessageArrivedEventArgs(ChatMessageArrivedEventArgs msgArgs) : base(msgArgs.Sender)
        {
            ChatMessage = new FFChatMsg(msgArgs.ChatMessage);
        }
    }
}
