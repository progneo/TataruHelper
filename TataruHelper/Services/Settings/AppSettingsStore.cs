namespace FFXIVTataruHelper.Services.Settings
{
    public sealed class AppSettingsStore : ISettingsStore
    {
        public string ChatCodesFilePath => GlobalSettings.ChatCodesFilePath;

        public string BlackListPath => GlobalSettings.BlackList;

        public string IgnoreNickNameChatCodesPath => GlobalSettings.IgnoreNickNameChatCodes;

        public string SettingsPath => GlobalSettings.Settings;

        public string OldSettingsPath => GlobalSettings.OldSettings;

        public int SettingsSaveDelayMs => GlobalSettings.SettingsSaveDelay;

        public int LookForProcessDelayMs => GlobalSettings.LookForPorcessDelay;

        public int MemoryReaderDelayMs => GlobalSettings.MemoryReaderDelay;

        public bool LoadGlobalSettings(string fileName)
        {
            return Helper.LoadStaticFromJson(typeof(GlobalSettings), fileName);
        }

        public void SaveGlobalSettings(string fileName)
        {
            Helper.SaveStaticToJson(typeof(GlobalSettings), fileName);
        }
    }
}
