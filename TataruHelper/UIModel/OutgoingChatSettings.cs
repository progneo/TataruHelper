using Translation.OutgoingChat;

namespace FFXIVTataruHelper.UIModel
{
    public class OutgoingChatSettings
    {
        public bool PrependChannelCommand { get; set; } = true;

        public bool AppendOriginalInParentheses { get; set; } = false;

        public bool RestoreClipboardAfterDelay { get; set; } = true;

        public int ClipboardRestoreDelaySeconds { get; set; } = 10;

        public ChatChannel DefaultChannel { get; set; } = ChatChannel.Say;

        public string LastTellTarget { get; set; } = string.Empty;

        public OutgoingChatSettings()
        {
        }

        public OutgoingChatSettings(OutgoingChatSettings other)
        {
            if (other == null)
            {
                return;
            }

            PrependChannelCommand = other.PrependChannelCommand;
            AppendOriginalInParentheses = other.AppendOriginalInParentheses;
            RestoreClipboardAfterDelay = other.RestoreClipboardAfterDelay;
            ClipboardRestoreDelaySeconds = other.ClipboardRestoreDelaySeconds;
            DefaultChannel = other.DefaultChannel;
            LastTellTarget = other.LastTellTarget;
        }
    }
}