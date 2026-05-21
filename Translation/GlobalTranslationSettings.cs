// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

namespace Translation
{
    public static class GlobalTranslationSettings
    {
        public static int TranslationCacheSize = 10000;

        public static int HttpRequestTimeoutMilliseconds = 10000;

        public static int HttpReadWriteTimeoutMilliseconds = 30000;

        public static int HttpRequestRetryCount = 2;

        public static int HttpRequestRetryDelayMilliseconds = 750;

        public static int TranslationRetryCount = 2;

        public static int TranslationRetryDelayMilliseconds = 500;

        public static double MaxSameLanguagePercent = 0.40;

        public static bool UseGoogleJsonEndpoint = true;

        public static bool UseGoogleHtmlFallbackEndpoint = true;

        public static string NTextCatLanguageModelsPath = "TranslationResources/Core14.profile.xml";

        public static string PapagoEncoderPath = "TranslationResources/PapagoEncoder";

        public static string PapagoKeyCachePath = "PapagoKey.cache";

        public static string GoogleTranslateLanguages = "TranslationResources/GoogleTranslateLanguages.json";

        public static string PapagoLanguages = "TranslationResources/PapagoLanguages.json";

        public static string AzureTranslatorLanguages = "TranslationResources/AzureTranslatorLanguages.json";

        public static string GoogleCloudTranslateLanguages = "TranslationResources/GoogleTranslateLanguages.json";

        public static string DeepLApiLanguages = "TranslationResources/DeeplLanguages.json";

        public static string OpenAILanguages = "TranslationResources/GoogleTranslateLanguages.json";

        public static string DeepSeekLanguages = "TranslationResources/GoogleTranslateLanguages.json";

        public static string YandexGptLanguages = "TranslationResources/GoogleTranslateLanguages.json";

        public static string YandexLanguages = "TranslationResources/YandexTranslateLanguages.json";

        public static string YandexCloudLanguages = "TranslationResources/YandexCloudLanguages.json";

        public static string YandexAuthFile = "TranslationResources/YandexAuth";

        public static string YandexUsersFile = "TranslationResources/YandexUsers.json";

        public static string YandexEncoderPath = "TranslationResources/YandexEncoder";
    }
}