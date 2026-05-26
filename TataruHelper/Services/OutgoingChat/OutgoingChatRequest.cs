using Translation.Core;
using Translation.OutgoingChat;

namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public sealed class OutgoingChatRequest
    {
        public string Text { get; set; }

        public ChatChannel Channel { get; set; }

        public string TellTarget { get; set; }

        public TranslationEngine Engine { get; set; }

        public TranslatorLanguague FromLanguage { get; set; }

        public TranslatorLanguague ToLanguage { get; set; }

        public bool PrependChannelCommand { get; set; }

        public bool AppendOriginalInParentheses { get; set; }

        public bool RestoreClipboardAfterDelay { get; set; }

        public int ClipboardRestoreDelaySeconds { get; set; }
    }
}