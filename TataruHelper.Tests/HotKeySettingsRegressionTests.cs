using System.ComponentModel;
using System.Threading.Tasks;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;

using NUnit.Framework;

namespace TataruHelper.Tests
{
    public class HotKeySettingsRegressionTests
    {
        [Test]
        public void WinId_Update_UsesClearChatPrefixForClearHotKey()
        {
            var settings = new ChatWindowViewModelSettings();

            settings.WinId = 7;

            Assert.That(settings.ClearChatKeys.Name, Is.EqualTo("ClearChatKeys7"));
            Assert.That(settings.ClearChatKeys.Name, Is.Not.EqualTo(settings.ClickThoughtChatKeys.Name));
        }

        [Test]
        public async Task StopAsync_PersistsPendingChanges_WhenDebounceHasNotElapsed()
        {
            var source = new FakeNotifyPropertyChanged();
            var settingsStore = new FakeSettingsStore();
            var logger = new NullLogger();
            var service = new SettingsSyncService(settingsStore, logger);
            var persistCount = 0;

            service.Start(source, () =>
            {
                persistCount++;
                return Task.CompletedTask;
            });

            source.Raise(nameof(FakeNotifyPropertyChanged.Value));
            await service.StopAsync();

            Assert.That(persistCount, Is.EqualTo(1));
        }

        private sealed class FakeNotifyPropertyChanged : INotifyPropertyChanged
        {
            public event PropertyChangedEventHandler PropertyChanged;

            public int Value { get; set; }

            public void Raise(string propertyName)
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
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