using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Settings;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.Settings
{
    // The reset takes the saved settings away rather than writing defaults over
    // them, because the load is the only thing that knows what a complete set
    // looks like - it fills in a chat window when there are none, adds chat
    // codes a newer build has learned, and numbers the windows.
    [TestFixture]
    public class SettingsResetServiceTests
    {
        private string _folder;
        private FakeStore _store;
        private FakeSync _sync;
        private SettingsResetService _service;

        [SetUp]
        public void SetUp()
        {
            _folder = Path.Combine(Path.GetTempPath(), "TataruReset_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_folder);

            _store = new FakeStore(
                Path.Combine(_folder, "UserSettingsNew.json"),
                Path.Combine(_folder, "UserSettings.json"));

            _sync = new FakeSync();
            _service = new SettingsResetService(_store, _sync, new NullLogger());
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_folder, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public async Task BothSavedFiles_AreTakenAway()
        {
            File.WriteAllText(_store.SettingsPath, "{}");
            File.WriteAllText(_store.OldSettingsPath, "{}");

            await _service.ResetAsync();

            Assert.Multiple(() =>
            {
                Assert.That(File.Exists(_store.SettingsPath), Is.False);
                Assert.That(File.Exists(_store.OldSettingsPath), Is.False);
            });
        }

        [Test]
        public async Task TheWatcherIsStoppedFirst()
        {
            File.WriteAllText(_store.SettingsPath, "{}");

            await _service.ResetAsync();

            // Deleting while it still watches would have the next change put
            // the file straight back.
            Assert.That(_sync.StoppedBeforeDelete, Is.True);
        }

        [Test]
        public async Task AfterAReset_TheSettingsMustNotBeSaved()
        {
            // The application saves on its way out. After a reset that save
            // would put every setting back, and the button would appear to do
            // nothing at all.
            Assert.That(_service.WasReset, Is.False);

            await _service.ResetAsync();

            Assert.That(_service.WasReset, Is.True);
        }

        [Test]
        public async Task NothingToRemove_IsNotAFailure()
        {
            Assert.DoesNotThrowAsync(() => _service.ResetAsync());
            await Task.CompletedTask;
        }

        [Test]
        public void WhatItRemoves_IsTheUsersOwnSettingsOnly()
        {
            // Not the API keys, which somebody typed in and would have to find
            // again, and not the system settings, which are paths and timeouts.
            Assert.That(_service.Removes, Is.EquivalentTo(new[] { _store.SettingsPath, _store.OldSettingsPath }));
        }

        private sealed class FakeStore : ISettingsStore
        {
            public FakeStore(string settingsPath, string oldSettingsPath)
            {
                SettingsPath = settingsPath;
                OldSettingsPath = oldSettingsPath;
            }

            public string SettingsPath { get; }
            public string OldSettingsPath { get; }

            public AppSettings AppSettings => new AppSettings();
            public string ChatCodesFilePath => string.Empty;
            public string BlackListPath => string.Empty;
            public string IgnoreNickNameChatCodesPath => string.Empty;
            public string SystemSettingsPath => string.Empty;
            public int SettingsSaveDelayMs => 0;
            public int LookForProcessDelayMs => 0;
            public int MemoryReaderDelayMs => 0;
            public int AutoHideWatcherDelayMs => 0;
            public int TranslatorWaitTimeMs => 0;
            public int MaxTranslateTryCount => 0;
            public int MaxChatMessages => 0;
            public bool LoadGlobalSettings(string fileName) => true;
            public void SaveGlobalSettings(string fileName) { }
        }

        private sealed class NullLogger : FFXIVTataruHelper.Services.Logging.IAppLogger
        {
            public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteConsoleLog(string input) { }
            public void WriteChatLog(string input) { }
        }

        private sealed class FakeSync : ISettingsSyncService
        {
            private bool _stopped;

            public bool StoppedBeforeDelete { get; private set; }

            public void Start(INotifyPropertyChanged settingsSource, Func<Task> persistSettingsAsync)
            {
            }

            public Task StopAsync(CancellationToken cancellationToken = default)
            {
                _stopped = true;
                StoppedBeforeDelete = _stopped;
                return Task.CompletedTask;
            }

            public void Dispose()
            {
            }
        }
    }
}
