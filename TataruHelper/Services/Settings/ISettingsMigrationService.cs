using Translation;

namespace FFXIVTataruHelper.Services.Settings
{
    public interface ISettingsMigrationService
    {
        UserSettings LoadUserSettings(string systemSettingsFileName, ChatProcessor chatProcessor, WebTranslator webTranslator);
    }
}
