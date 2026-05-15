namespace FFXIVTataruHelper.Services.Settings
{
    public interface ISettingsStore
    {
        string ChatCodesFilePath { get; }

        string BlackListPath { get; }

        string IgnoreNickNameChatCodesPath { get; }

        string SettingsPath { get; }

        string OldSettingsPath { get; }

        int SettingsSaveDelayMs { get; }

        int LookForProcessDelayMs { get; }

        int MemoryReaderDelayMs { get; }

        int AutoHideWatcherDelayMs { get; }

        int TranslatorWaitTimeMs { get; }

        int MaxTranslateTryCount { get; }

        bool LoadGlobalSettings(string fileName);

        void SaveGlobalSettings(string fileName);
    }
}
