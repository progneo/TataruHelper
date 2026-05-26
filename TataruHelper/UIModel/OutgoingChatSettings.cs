using System.Drawing;

using FFXIVTataruHelper.WinUtils;

using Translation.OutgoingChat;

using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

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

        public RectangleD WindowRect { get; set; } = new RectangleD(120, 120, 480, 140);

        public Color BackgroundColor { get; set; } = (Color)ColorConverter.ConvertFromString("#B0202020");

        public double WindowOpacity { get; set; } = 1.0;

        public HotKeyCombination ShowHideKey { get; set; } = new HotKeyCombination("ShowHideOutgoingChat");

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
            WindowRect = other.WindowRect;
            BackgroundColor = other.BackgroundColor;
            WindowOpacity = other.WindowOpacity;
            ShowHideKey = other.ShowHideKey != null
                ? new HotKeyCombination(other.ShowHideKey)
                : new HotKeyCombination("ShowHideOutgoingChat");
        }
    }
}