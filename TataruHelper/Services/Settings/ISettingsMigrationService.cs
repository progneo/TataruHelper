using System.Collections.Generic;

using Translation.Models;

namespace FFXIVTataruHelper.Services.Settings
{
    public interface ISettingsMigrationService
    {
        UserSettings LoadUserSettings(string systemSettingsFileName, IReadOnlyList<ChatMsgType> allChatCodes,
            IReadOnlyList<TranslationEngine> translationEngines);
    }
}