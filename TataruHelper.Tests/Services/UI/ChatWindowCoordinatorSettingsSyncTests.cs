using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

using Translation;

using Color = System.Windows.Media.Color;

namespace TataruHelper.Tests
{
    public class ChatWindowCoordinatorSettingsSyncTests
    {
        [Test]
        public void AddFromViewModel_BindsLayoutPropertiesToSettings()
        {
            var logger = new NullLogger();
            var uiDispatcher = new ImmediateUiDispatcher();
            var coordinator = new ChatWindowCoordinator(uiDispatcher, logger, new DummyChatWindowFactory());
            var uiModel = new TataruUIModel(new FakeSettingsStore(), uiDispatcher, logger);

            var settings = CreateBaseSettings();
            var viewModel = CreateViewModel(settings, logger);

            try
            {
                coordinator.AddFromViewModel(viewModel, uiModel);

                Assert.That(uiModel.ChatWindows.Count, Is.EqualTo(1));
                var syncedSettings = uiModel.ChatWindows[0];

                viewModel.WindowCornerRadius = 4;
                viewModel.ContentPadding = 18;
                viewModel.MessagesInContainer = true;
                viewModel.MessageContainerPadding = 10;
                viewModel.MessageContainerAlpha = 64;
                viewModel.MessageContainerBorderThickness = 2;
                viewModel.MessageContainerBorderAlpha = 140;
                viewModel.ShowOnlyLastMessage = true;

                var synced = SpinWait.SpinUntil(() =>
                        syncedSettings.WindowCornerRadius == 4
                        && syncedSettings.ContentPadding == 18
                        && syncedSettings.MessagesInContainer
                        && syncedSettings.MessageContainerPadding == 10
                        && syncedSettings.MessageContainerAlpha == 64
                        && syncedSettings.MessageContainerBorderThickness == 2
                        && syncedSettings.MessageContainerBorderAlpha == 140
                        && syncedSettings.ShowOnlyLastMessage,
                    TimeSpan.FromSeconds(1));

                Assert.That(synced, Is.True, "Chat window layout settings were not synchronized to settings model.");
            }
            finally
            {
                viewModel.Dispose();
            }
        }

        private static ChatWindowViewModelSettings CreateBaseSettings()
        {
            var settings = new ChatWindowViewModelSettings("1", 0);
            var languages = new List<TranslatorLanguague>
            {
                new("Auto", "Auto", "auto"), new("English", "English", "en")
            };
            settings.FromLanguague = languages[0];
            settings.ToLanguague = languages[1];
            return settings;
        }

        private static ChatWindowViewModel CreateViewModel(ChatWindowViewModelSettings settings, IAppLogger logger)
        {
            var languages = new List<TranslatorLanguague>
            {
                new("Auto", "Auto", "auto"), new("English", "English", "en")
            };

            var translationEngines = new List<TranslationEngine>
            {
                new(TranslationEngineName.GoogleTranslate, languages, 1.0)
            };

            var allChatCodes = new List<ChatMsgType>
            {
                new("0039", MsgType.Translate, "System", Color.FromArgb(255, 255, 255, 255))
            };

            var hotKeyManager = new HotKeyManager(null);
            var hotKeyBindingService = new HotKeyBindingService(logger);

            return new ChatWindowViewModel(
                settings,
                translationEngines,
                null,
                allChatCodes,
                hotKeyManager,
                logger,
                hotKeyBindingService);
        }

        private sealed class ImmediateUiDispatcher : IUiDispatcher
        {
            public bool IsInitialized => true;

            public Window CurrentWindow => null;

            public void SetWindow(Window window)
            {
            }

            public void Invoke(Action action)
            {
                action();
            }

            public Task InvokeAsync(Action action)
            {
                action();
                return Task.CompletedTask;
            }
        }

        private sealed class DummyChatWindowFactory : IChatWindowFactory
        {
            public ChatWindow Create(TataruModel tataruModel, ChatWindowViewModel chatWindowViewModel,
                MainWindow mainWindow)
            {
                throw new NotSupportedException("Not used in this test.");
            }
        }

        private sealed class FakeSettingsStore : ISettingsStore
        {
            public string ChatCodesFilePath => string.Empty;
            public string BlackListPath => string.Empty;
            public string IgnoreNickNameChatCodesPath => string.Empty;
            public string SystemSettingsPath => string.Empty;
            public string SettingsPath => string.Empty;
            public string OldSettingsPath => string.Empty;
            public int SettingsSaveDelayMs => 60_000;
            public int LookForProcessDelayMs => 1;
            public int MemoryReaderDelayMs => 1;
            public int AutoHideWatcherDelayMs => 1;
            public int TranslatorWaitTimeMs => 1;
            public int MaxTranslateTryCount => 1;
            public int MaxChatMessages => 500;

            public bool LoadGlobalSettings(string fileName)
            {
                return true;
            }

            public void SaveGlobalSettings(string fileName)
            {
            }
        }

        private sealed class NullLogger : IAppLogger
        {
            public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0)
            {
            }

            public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0)
            {
            }

            public void WriteConsoleLog(string input)
            {
            }

            public void WriteChatLog(string input)
            {
            }
        }
    }
}