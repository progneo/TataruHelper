using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Media;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

using Translation.Models;

namespace TataruHelper.Tests.Services.Settings
{
    [TestFixture]
    public class SettingsMigrationServiceTests
    {
        private string _tempDir;
        private FakeSettingsStore _settingsStore;
        private SettingsMigrationService _service;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TataruMigrationTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);

            _settingsStore = new FakeSettingsStore
            {
                SettingsPath = Path.Combine(_tempDir, "UserSettingsNew.json"),
                OldSettingsPath = Path.Combine(_tempDir, "UserSettings.json"),
            };
            _service = new SettingsMigrationService(_settingsStore, new NullLogger());
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void LoadUserSettings_NoSettingsFiles_ReturnsDefaults()
        {
            var userSettings = _service.LoadUserSettings("sys.json", NoChatCodes(), NoEngines());

            Assert.That(userSettings, Is.Not.Null);
            Assert.That(userSettings.ChatWindows, Is.Empty);
        }

        [Test]
        public void LoadUserSettings_GlobalSettingsLoadFails_SavesDefaultsAndRetries()
        {
            _settingsStore.LoadGlobalSettingsResult = false;

            _service.LoadUserSettings("sys.json", NoChatCodes(), NoEngines());

            Assert.That(_settingsStore.SaveGlobalSettingsCalls, Is.EqualTo(new[] { "sys.json" }));
            Assert.That(_settingsStore.LoadGlobalSettingsCalls, Is.EqualTo(new[] { "sys.json", "sys.json" }));
        }

        [Test]
        public void LoadUserSettings_AssignsSequentialWinIdsAndDefaultNames()
        {
            var settingsOnDisk = new UserSettings();
            settingsOnDisk.ChatWindows.Add(new ChatWindowViewModelSettings("First", 7));
            settingsOnDisk.ChatWindows.Add(new ChatWindowViewModelSettings(" ", 9));
            Helper.SaveJson(settingsOnDisk, _settingsStore.SettingsPath);

            var userSettings = _service.LoadUserSettings("sys.json", NoChatCodes(), NoEngines());

            Assert.That(userSettings.ChatWindows.Select(x => x.WinId), Is.EqualTo(new long[] { 0, 1 }));
            Assert.That(userSettings.ChatWindows[0].Name, Is.EqualTo("First"));
            Assert.That(userSettings.ChatWindows[1].Name, Is.EqualTo("2"));
        }

        [Test]
        public void LoadUserSettings_ChatCodeCountMismatch_RebuildsCodesPreservingCheckedState()
        {
            var allChatCodes = new List<ChatMsgType>
            {
                new ChatMsgType("003D", MsgType.Translate, "NPCD", Colors.Green),
                new ChatMsgType("0044", MsgType.Translate, "NPCA", Colors.Blue),
            };

            var window = new ChatWindowViewModelSettings("1", 0)
            {
                ChatCodes = new List<ChatCodeViewModel>
                {
                    new ChatCodeViewModel("003D", "NPCD", Colors.Red, true),
                },
            };
            var settingsOnDisk = new UserSettings();
            settingsOnDisk.ChatWindows.Add(window);
            Helper.SaveJson(settingsOnDisk, _settingsStore.SettingsPath);

            var userSettings = _service.LoadUserSettings("sys.json", allChatCodes, NoEngines());

            var codes = userSettings.ChatWindows[0].ChatCodes;
            Assert.That(codes.Select(x => x.Code), Is.EqualTo(new[] { "003D", "0044" }));
            Assert.That(codes.Single(x => x.Code == "003D").IsChecked, Is.True);
            Assert.That(codes.Single(x => x.Code == "0044").IsChecked, Is.False);
        }

        [Test]
        public void LoadUserSettings_MatchingChatCodeCount_KeepsExistingCodes()
        {
            var allChatCodes = new List<ChatMsgType>
            {
                new ChatMsgType("003D", MsgType.Translate, "NPCD", Colors.Green),
            };

            var window = new ChatWindowViewModelSettings("1", 0)
            {
                ChatCodes = new List<ChatCodeViewModel>
                {
                    new ChatCodeViewModel("CUSTOM", "Custom", Colors.Red, true),
                },
            };
            var settingsOnDisk = new UserSettings();
            settingsOnDisk.ChatWindows.Add(window);
            Helper.SaveJson(settingsOnDisk, _settingsStore.SettingsPath);

            var userSettings = _service.LoadUserSettings("sys.json", allChatCodes, NoEngines());

            Assert.That(userSettings.ChatWindows[0].ChatCodes.Single().Code, Is.EqualTo("CUSTOM"));
        }

        [Test]
        public void LoadUserSettings_OldSettingsFile_MigratesToFirstChatWindowAndDeletesFile()
        {
            var english = new TranslatorLanguage("English", "English", "en");
            var engines = new List<TranslationEngine>
            {
                new TranslationEngine(TranslationEngineName.GoogleTranslate,
                    new List<TranslatorLanguage> { english }, 1.0),
            };

            var oldSettings = new UserSettingsOld
            {
                FontSize = 21,
                CurrentTranslationEngine = (int)TranslationEngineName.GoogleTranslate,
                CurrentFFXIVLanguage = "English",
                CurrentTranslateToLanguage = "English",
            };
            Helper.SaveJson(oldSettings, _settingsStore.OldSettingsPath);
            Helper.SaveJson(new UserSettings(), _settingsStore.SettingsPath);

            var userSettings = _service.LoadUserSettings("sys.json", NoChatCodes(), engines);

            Assert.That(File.Exists(_settingsStore.OldSettingsPath), Is.False);
            Assert.That(userSettings.ChatWindows, Has.Count.EqualTo(1));

            var migrated = userSettings.ChatWindows[0];
            Assert.That(migrated.ChatFontSize, Is.EqualTo(21));
            Assert.That(migrated.TranslationEngineName, Is.EqualTo(TranslationEngineName.GoogleTranslate));
            Assert.That(migrated.FromLanguague.ShownName, Is.EqualTo("English"));
            Assert.That(migrated.ToLanguague.ShownName, Is.EqualTo("English"));
        }

        [Test]
        public void LoadUserSettings_OldSettingsFile_ExistingChatWindowsAreKept()
        {
            Helper.SaveJson(new UserSettingsOld(), _settingsStore.OldSettingsPath);

            var settingsOnDisk = new UserSettings();
            settingsOnDisk.ChatWindows.Add(new ChatWindowViewModelSettings("Existing", 0));
            Helper.SaveJson(settingsOnDisk, _settingsStore.SettingsPath);

            var userSettings = _service.LoadUserSettings("sys.json", NoChatCodes(), NoEngines());

            Assert.That(File.Exists(_settingsStore.OldSettingsPath), Is.False);
            Assert.That(userSettings.ChatWindows, Has.Count.EqualTo(1));
            Assert.That(userSettings.ChatWindows[0].Name, Is.EqualTo("Existing"));
        }

        private static IReadOnlyList<ChatMsgType> NoChatCodes()
        {
            return new List<ChatMsgType>();
        }

        private static IReadOnlyList<TranslationEngine> NoEngines()
        {
            return new List<TranslationEngine>();
        }

        private sealed class FakeSettingsStore : ISettingsStore
        {
            public AppSettings AppSettings { get; } = new AppSettings();

            public string ChatCodesFilePath => string.Empty;
            public string BlackListPath => string.Empty;
            public string IgnoreNickNameChatCodesPath => string.Empty;
            public string SystemSettingsPath => string.Empty;
            public string SettingsPath { get; set; } = string.Empty;
            public string OldSettingsPath { get; set; } = string.Empty;
            public int SettingsSaveDelayMs => 1;
            public int LookForProcessDelayMs => 1;
            public int MemoryReaderDelayMs => 1;
            public int AutoHideWatcherDelayMs => 1;
            public int TranslatorWaitTimeMs => 1;
            public int MaxTranslateTryCount => 1;
            public int MaxChatMessages => 500;

            public bool LoadGlobalSettingsResult { get; set; } = true;
            public List<string> LoadGlobalSettingsCalls { get; } = new List<string>();
            public List<string> SaveGlobalSettingsCalls { get; } = new List<string>();

            public bool LoadGlobalSettings(string fileName)
            {
                LoadGlobalSettingsCalls.Add(fileName);
                return LoadGlobalSettingsResult;
            }

            public void SaveGlobalSettings(string fileName)
            {
                SaveGlobalSettingsCalls.Add(fileName);
            }
        }

        private sealed class NullLogger : IAppLogger
        {
            public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteConsoleLog(string input) { }
            public void WriteChatLog(string input) { }
        }
    }
}