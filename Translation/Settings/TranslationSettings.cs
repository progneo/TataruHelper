namespace Translation.Settings
{
    public sealed class TranslationSettings
    {
        public int TranslationCacheSize { get; set; } = 10000;

        public int HttpRequestTimeoutMilliseconds { get; set; } = 10000;

        public int HttpReadWriteTimeoutMilliseconds { get; set; } = 30000;

        public int HttpRequestRetryCount { get; set; } = 2;

        public int HttpRequestRetryDelayMilliseconds { get; set; } = 750;

        public int TranslationRetryCount { get; set; } = 2;

        public int TranslationRetryDelayMilliseconds { get; set; } = 500;

        public double MaxSameLanguagePercent { get; set; } = 0.40;

        public bool UseGoogleJsonEndpoint { get; set; } = true;

        public bool UseGoogleHtmlFallbackEndpoint { get; set; } = true;

        public string NTextCatLanguageModelsPath { get; set; } = "TranslationResources/Core14.profile.xml";

        public string PapagoEncoderPath { get; set; } = "TranslationResources/PapagoEncoder";

        public string PapagoKeyCachePath { get; set; } = "PapagoKey.cache";

        public string GoogleTranslateLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string PapagoLanguages { get; set; } = "TranslationResources/PapagoLanguages.json";

        public string AzureTranslatorLanguages { get; set; } = "TranslationResources/AzureTranslatorLanguages.json";

        public string GoogleCloudTranslateLanguages { get; set; } =
            "TranslationResources/GoogleTranslateLanguages.json";

        public string DeepLApiLanguages { get; set; } = "TranslationResources/DeeplLanguages.json";

        public string OpenAILanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string DeepSeekLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string YandexGptLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string YandexLanguages { get; set; } = "TranslationResources/YandexTranslateLanguages.json";

        public string YandexCloudLanguages { get; set; } = "TranslationResources/YandexCloudLanguages.json";

        public string YandexAuthFile { get; set; } = "TranslationResources/YandexAuth";

        public string YandexUsersFile { get; set; } = "TranslationResources/YandexUsers.json";

        public string YandexEncoderPath { get; set; } = "TranslationResources/YandexEncoder";
    }
}