using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.WinUtils;

using Translation;

namespace FFXIVTataruHelper.Services.Settings
{
    public sealed class SettingsMigrationService : ISettingsMigrationService
    {
        private readonly ISettingsStore _settingsStore;
        private readonly IAppLogger _logger;

        public SettingsMigrationService(ISettingsStore settingsStore, IAppLogger logger)
        {
            _settingsStore = settingsStore;
            _logger = logger;
        }

        public UserSettings LoadUserSettings(string systemSettingsFileName, ChatProcessor chatProcessor,
            WebTranslator webTranslator)
        {
            if (!_settingsStore.LoadGlobalSettings(systemSettingsFileName))
            {
                _settingsStore.SaveGlobalSettings(systemSettingsFileName);
                _settingsStore.LoadGlobalSettings(systemSettingsFileName);
            }

            var userSettings = Helper.LoadJsonData<UserSettings>(_settingsStore.SettingsPath);
            LoadOldSettings(userSettings, webTranslator);

            if (userSettings == null)
            {
                userSettings = new UserSettings();
                _logger.WriteLog("userSettings == null");
            }

            LoadMissingChatCodes(userSettings, chatProcessor);

            for (int i = 0; i < userSettings.ChatWindows.Count; i++)
            {
                userSettings.ChatWindows[i].WinId = i;
                if (string.IsNullOrWhiteSpace(userSettings.ChatWindows[i].Name))
                {
                    userSettings.ChatWindows[i].Name = Convert.ToString(i + 1);
                }
            }

            return userSettings;
        }

        private void LoadMissingChatCodes(UserSettings userSettings, ChatProcessor chatProcessor)
        {
            foreach (var win in userSettings.ChatWindows)
            {
                if (win.ChatCodes.Count != chatProcessor.AllChatCodes.Count)
                {
                    var newUserChatCodes = new List<ChatCodeViewModel>(chatProcessor.AllChatCodes.Count);

                    foreach (var code in chatProcessor.AllChatCodes)
                    {
                        var userCode = win.ChatCodes.FirstOrDefault(x => x.Code == code.ChatCode);
                        bool isChecked = false;
                        if (userCode != null)
                        {
                            isChecked = userCode.IsChecked;
                        }

                        newUserChatCodes.Add(new ChatCodeViewModel(code.ChatCode, code.Name, code.Color, isChecked));
                    }

                    win.ChatCodes = newUserChatCodes;
                }
            }
        }

        private void LoadOldSettings(UserSettings userSettings, WebTranslator webTranslator)
        {
            if (!File.Exists(_settingsStore.OldSettingsPath))
            {
                return;
            }

            try
            {
                var oldSettings = Helper.LoadJsonData<UserSettingsOld>(_settingsStore.OldSettingsPath);

                try
                {
                    File.Delete(_settingsStore.OldSettingsPath);
                }
                catch (Exception ex)
                {
                    _logger.WriteLog(ex);
                }

                if (userSettings.ChatWindows.Count == 0)
                {
                    ChatWindowViewModelSettings windowSettings = new("1", 0)
                    {
                        ChatFontSize = oldSettings.FontSize,
                        LineBreakHeight = oldSettings.LineBreakHeight,
                        SpacingCount = oldSettings.InsertSpaceCount,
                        IsAlwaysOnTop = oldSettings.IsAlwaysOnTop,
                        IsClickThrough = oldSettings.IsClickThrough,
                        IsAutoHide = oldSettings.IsAutoHide,
                        AutoHideTimeout = oldSettings.AutoHideTimeout,
                        BackGroundColor = oldSettings.BackgroundColor
                    };

                    windowSettings.IsAutoHide = oldSettings.IsAutoHide;

                    windowSettings.TranslationEngineName = (TranslationEngineName)oldSettings.CurrentTranslationEngine;

                    var engine = webTranslator.TranslationEngines.FirstOrDefault(x =>
                        x.EngineName == windowSettings.TranslationEngineName);
                    if (engine != null)
                    {
                        var lang1 = engine.SupportedLanguages.FirstOrDefault(x =>
                            x.ShownName == oldSettings.CurrentFFXIVLanguage);
                        var lang2 = engine.SupportedLanguages.FirstOrDefault(x =>
                            x.ShownName == oldSettings.CurrentTranslateToLanguage);
                        if (lang1 != null && lang2 != null)
                        {
                            windowSettings.FromLanguague = new TranslatorLanguague(lang1);
                            windowSettings.ToLanguague = new TranslatorLanguague(lang2);
                        }
                    }

                    windowSettings.ChatWindowRectangle = oldSettings.ChatWindowLocation;

                    foreach (var chatCode in windowSettings.ChatCodes)
                    {
                        ChatMsgType msgType = null;
                        if (oldSettings.ChatCodes.TryGetValue(chatCode.Code, out msgType))
                        {
                            bool isChecked = msgType.MsgType == MsgType.Translate;

                            chatCode.IsChecked = isChecked;
                            chatCode.Color = msgType.Color;
                        }
                    }

                    windowSettings.ShowHideChatKeys =
                        new HotKeyCombination(oldSettings.ShowHideChatKeys.Name + "0",
                            oldSettings.ShowHideChatKeys);
                    windowSettings.ClickThoughtChatKeys =
                        new HotKeyCombination(oldSettings.ClickThoughtChatKeys.Name + "0",
                            oldSettings.ClickThoughtChatKeys);
                    windowSettings.ClearChatKeys = new HotKeyCombination(oldSettings.ClearChatKeys.Name + "0",
                        oldSettings.ClearChatKeys);

                    userSettings.ChatWindows.Add(new ChatWindowViewModelSettings(windowSettings));
                }
            }
            catch (Exception ex)
            {
                _logger.WriteLog(ex);
            }
        }
    }
}