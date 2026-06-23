using System.Linq;

using FFXIVTataruHelper.WinUtils;

using Translation.Models;

namespace FFXIVTataruHelper.ViewModel
{
    internal static class ChatWindowSettingsMapper
    {
        public static void ApplyDisplaySettings(ChatWindowViewModel viewModel, ChatWindowViewModelSettings settings)
        {
            viewModel.Name = settings.Name;

            viewModel.ChatFontSize = settings.ChatFontSize;
            viewModel.LineBreakHeight = settings.LineBreakHeight;
            viewModel.SpacingCount = settings.SpacingCount;

            viewModel.ChatFont = settings.ChatFont;

            viewModel.IsAlwaysOnTop = settings.IsAlwaysOnTop;
            viewModel.IsClickThrough = settings.IsClickThrough;
            viewModel.IsAutoHide = settings.IsAutoHide;

            viewModel.AutoHideTimeout = settings.AutoHideTimeout;

            viewModel.IsHiddenByUser = false;

            viewModel.ShowTimestamps = settings.ShowTimestamps;

            viewModel.WindowCornerRadius = settings.WindowCornerRadius > 0 ? settings.WindowCornerRadius : 12;
            viewModel.ContentPadding = settings.ContentPadding;
            viewModel.MessagesInContainer = settings.MessagesInContainer;
            viewModel.MessageContainerPadding = settings.MessageContainerPadding;
            viewModel.MessageContainerAlpha = settings.MessageContainerAlpha;
            viewModel.MessageContainerBorderThickness = settings.MessageContainerBorderThickness;
            viewModel.MessageContainerBorderAlpha = settings.MessageContainerBorderAlpha;
            viewModel.ShowOnlyLastMessage = settings.ShowOnlyLastMessage;

            viewModel.BackGroundColor = settings.BackGroundColor;

            viewModel.ChatWindowRectangle = settings.ChatWindowRectangle;
        }

        public static ChatWindowViewModelSettings ToSettings(ChatWindowViewModel viewModel)
        {
            ChatWindowViewModelSettings settings = new ChatWindowViewModelSettings();

            settings.Name = viewModel.Name;
            settings.WinId = viewModel.WinId;

            settings.ChatFontSize = viewModel.ChatFontSize;
            settings.LineBreakHeight = viewModel.LineBreakHeight;
            settings.SpacingCount = viewModel.SpacingCount;

            settings.IsAlwaysOnTop = viewModel.IsAlwaysOnTop;
            settings.IsClickThrough = viewModel.IsClickThrough;
            settings.IsAutoHide = viewModel.IsAutoHide;

            settings.AutoHideTimeout = viewModel.AutoHideTimeout;

            settings.BackGroundColor = viewModel.BackGroundColor;

            settings.WindowCornerRadius = viewModel.WindowCornerRadius;
            settings.ShowTimestamps = viewModel.ShowTimestamps;
            settings.ContentPadding = viewModel.ContentPadding;
            settings.MessagesInContainer = viewModel.MessagesInContainer;
            settings.MessageContainerPadding = viewModel.MessageContainerPadding;
            settings.MessageContainerAlpha = viewModel.MessageContainerAlpha;
            settings.MessageContainerBorderThickness = viewModel.MessageContainerBorderThickness;
            settings.MessageContainerBorderAlpha = viewModel.MessageContainerBorderAlpha;
            settings.ShowOnlyLastMessage = viewModel.ShowOnlyLastMessage;

            settings.TranslationEngineName =
                viewModel.SelectedEngine?.EngineName ?? TranslationEngineName.GoogleTranslate;

            settings.FromLanguague = (TranslatorLanguage)viewModel.TranslateFromLanguages.CurrentItem;
            settings.ToLanguague = (TranslatorLanguage)viewModel.TranslateToLanguages.CurrentItem;

            settings.ChatWindowRectangle = viewModel.ChatWindowRectangle;

            settings.ChatCodes = viewModel.ChatCodes.Select(entry => new ChatCodeViewModel(entry)).ToList();

            settings.ShowHideChatKeys = new HotKeyCombination(viewModel.ShowHideChatKeys);
            settings.ClickThoughtChatKeys = new HotKeyCombination(viewModel.ClickThoughtChatKeys);
            settings.ClearChatKeys = new HotKeyCombination(viewModel.ClearChatKeys);

            return settings;
        }
    }
}