namespace FFXIVTataruHelper.Services.Settings
{
    public interface ISettingsStore
    {
        AppSettings AppSettings { get; }

        string ChatCodesFilePath { get; }

        string BlackListPath { get; }

        string IgnoreNickNameChatCodesPath { get; }

        string SystemSettingsPath { get; }

        string SettingsPath { get; }

        string OldSettingsPath { get; }

        int SettingsSaveDelayMs { get; }

        int LookForProcessDelayMs { get; }

        int MemoryReaderDelayMs { get; }

        int AutoHideWatcherDelayMs { get; }

        int TranslatorWaitTimeMs { get; }

        int MaxTranslateTryCount { get; }

        int MaxChatMessages { get; }

        bool LoadGlobalSettings(string fileName);

        void SaveGlobalSettings(string fileName);
    }
}