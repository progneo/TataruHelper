namespace FFXIVTataruHelper
{
    // Application-level configuration persisted to AppSysSettings.json.
    // Property names must stay aligned with the legacy on-disk format: the file
    // stores [name, value] pairs produced from the old GlobalSettings static
    // fields, so the names (including historical misspellings) are frozen.
    public sealed class AppSettings
    {
        public string ChatCodesFilePath { get; set; } = @"Resources\ChatCodes.json";

        public string LocalisationDirPath { get; set; } = @"Locale\";

        // Catalog layout mirrors Crowdin's %locale% exactly - folder and file
        // alike - so a downloaded translation lands where the app already looks
        // and no placeholder mapping is needed on either side.
        public string ru_RU_LanguaguePath { get; set; } = @"ru-RU\ru-RU.mo";

        public string en_US_LanguaguePath { get; set; } = @"en-US\en-US.mo";

        public string es_ES_LanguaguePath { get; set; } = @"es-ES\es-ES.mo";

        public string pl_PL_LanguaguePath { get; set; } = @"pl-PL\pl-PL.mo";

        public string ko_KR_LanguaguePath { get; set; } = @"ko-KR\ko-KR.mo";

        public string pt_BR_LanguaguePath { get; set; } = @"pt-BR\pt-BR.mo";

        public string ca_Es_LanguaguePath { get; set; } = @"ca-ES\ca-ES.mo";

        public string it_IT_LanguaguePath { get; set; } = @"it-IT\it-IT.mo";

        public string uk_UA_LanguaguePath { get; set; } = @"uk-UA\uk-UA.mo";

        public string zh_CN_LanguaguePath { get; set; } = @"zh-CN\zh-CN.mo";

        public string zh_TR_LanguaguePath { get; set; } = @"zh-TW\zh-TW.mo";

        public string ja_LanguaguePath { get; set; } = @"ja-JP\ja-JP.mo";

        public int SpiWaitTimeOutMS { get; set; } = 500;

        public int LookForPorcessDelay { get; set; } = 500;

        public int AutoHideWatcherDelay { get; set; } = 500;

        public int AutoTimeOutHideMinValueSeconds { get; set; } = 1;

        public int MemoryReaderDelay { get; set; } = 33;

        public int MaxСonsecutiveNotFromLogSentences { get; set; } = 100000;

        public int TranslationDelay { get; set; } = 33;

        public int TranslatorWaitTime { get; set; } = 30000;

        public int TranslationContextBufferWindowMs { get; set; } = 300;

        public int TranslationContextMaxBatchSize { get; set; } = 4;

        public string TranslationContextBatchDelimiter { get; set; } = "\n<<<TATARU_TRANSLATION_SEGMENT>>>\n";

        public int SettingsSaveDelay { get; set; } = 2500;

        public int MaxTranslateTryCount { get; set; } = 4;

        public int MaxChatMessages { get; set; } = 500;

        public string OldSettings { get; set; } = "../UserSettings.json";

        public string Settings { get; set; } = "../UserSettingsNew.json";

        public string BlackList { get; set; } = @"Resources\MsgBlackList.json";

        public string IgnoreNickNameChatCodes { get; set; } = @"Resources\IgnoreNickNameChatCodes.json";
    }
}
