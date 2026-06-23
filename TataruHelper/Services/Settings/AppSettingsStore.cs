using System;
using System.IO;

namespace FFXIVTataruHelper.Services.Settings
{
    public sealed class AppSettingsStore : ISettingsStore
    {
        private const string AppDataFolderName = "TataruHelper";
        private const string UserSettingsFileName = "UserSettingsNew.json";
        private const string LegacyUserSettingsFileName = "UserSettings.json";
        private const string SystemSettingsFileName = "AppSysSettings.json";

        private readonly string _appDataDirectory;
        private readonly string _baseDirectory;
        private readonly string _systemSettingsPath;
        private readonly string _settingsPath;
        private readonly string _oldSettingsPath;

        public AppSettingsStore() : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), AppDataFolderName),
            AppContext.BaseDirectory)
        {
        }

        public AppSettingsStore(string appDataDirectory, string baseDirectory)
        {
            _appDataDirectory = appDataDirectory;
            _baseDirectory = baseDirectory;

            Directory.CreateDirectory(_appDataDirectory);

            _systemSettingsPath = Path.Combine(_appDataDirectory, SystemSettingsFileName);
            _settingsPath = Path.Combine(_appDataDirectory, UserSettingsFileName);
            _oldSettingsPath = Path.Combine(_appDataDirectory, LegacyUserSettingsFileName);

            MigrateLegacyUserSettingsIfNeeded();
        }

        public AppSettings AppSettings { get; private set; } = new AppSettings();

        public string ChatCodesFilePath => ResolveBaseDirectoryPath(AppSettings.ChatCodesFilePath);

        public string BlackListPath => ResolveBaseDirectoryPath(AppSettings.BlackList);

        public string IgnoreNickNameChatCodesPath => ResolveBaseDirectoryPath(AppSettings.IgnoreNickNameChatCodes);

        public string SystemSettingsPath => _systemSettingsPath;

        public string SettingsPath => _settingsPath;

        public string OldSettingsPath => _oldSettingsPath;

        public int SettingsSaveDelayMs => AppSettings.SettingsSaveDelay;

        public int LookForProcessDelayMs => AppSettings.LookForPorcessDelay;

        public int MemoryReaderDelayMs => AppSettings.MemoryReaderDelay;

        public int AutoHideWatcherDelayMs => AppSettings.AutoHideWatcherDelay;

        public int TranslatorWaitTimeMs => AppSettings.TranslatorWaitTime;

        public int MaxTranslateTryCount => AppSettings.MaxTranslateTryCount;

        public int MaxChatMessages => AppSettings.MaxChatMessages;

        public bool LoadGlobalSettings(string fileName)
        {
            var loaded = LegacySettingsStorage.Load<AppSettings>(ResolveGlobalSettingsPath(fileName));
            if (loaded == null)
                return false;

            AppSettings = loaded;
            return true;
        }

        public void SaveGlobalSettings(string fileName)
        {
            LegacySettingsStorage.Save(AppSettings, ResolveGlobalSettingsPath(fileName));
        }

        private string ResolveGlobalSettingsPath(string fileName)
        {
            if (Path.IsPathRooted(fileName))
            {
                return fileName;
            }

            return Path.Combine(_appDataDirectory, fileName);
        }

        private string ResolveBaseDirectoryPath(string path)
        {
            if (Path.IsPathRooted(path))
            {
                return path;
            }

            return Path.GetFullPath(Path.Combine(_baseDirectory, path));
        }

        private void MigrateLegacyUserSettingsIfNeeded()
        {
            if (File.Exists(_settingsPath))
            {
                Logger.WriteLog($"Settings migration skipped: target settings already exists at '{_settingsPath}'.");
                return;
            }

            var legacyCandidates = new[]
            {
                ResolveBaseDirectoryPath(AppSettings.Settings),
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, AppSettings.Settings))
            };

            foreach (var legacyPath in legacyCandidates)
            {
                if (TryMoveLegacyFile(legacyPath, _settingsPath))
                {
                    Logger.WriteLog($"Settings migration succeeded: copied '{legacyPath}' to '{_settingsPath}'.");
                    return;
                }
            }

            Logger.WriteLog("Settings migration: no legacy current settings file found.");

            var legacyOldCandidates = new[]
            {
                ResolveBaseDirectoryPath(AppSettings.OldSettings),
                Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, AppSettings.OldSettings))
            };

            foreach (var legacyPath in legacyOldCandidates)
            {
                if (TryMoveLegacyFile(legacyPath, _oldSettingsPath))
                {
                    Logger.WriteLog(
                        $"Settings migration copied legacy old settings '{legacyPath}' to '{_oldSettingsPath}'.");
                    return;
                }
            }

            Logger.WriteLog("Settings migration: no legacy old settings file found.");
        }

        private static bool TryMoveLegacyFile(string sourcePath, string targetPath)
        {
            if (!File.Exists(sourcePath))
            {
                return false;
            }

            var targetDirectory = Path.GetDirectoryName(targetPath);
            if (!string.IsNullOrEmpty(targetDirectory))
            {
                Directory.CreateDirectory(targetDirectory);
            }

            File.Copy(sourcePath, targetPath, true);
            return true;
        }
    }
}