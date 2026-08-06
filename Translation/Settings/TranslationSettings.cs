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

        /// <summary>
        /// Index of translations made by hand. Consulted before a translator
        /// when the literary translation option is on, and fetched by the
        /// update button on the General page.
        ///
        /// Kept with the user's settings rather than with the application: the
        /// application is replaced wholesale when it updates, and an index
        /// beside it would go with it. Nothing ships one - it is several
        /// hundred megabytes that the translation project adds to weekly - so
        /// until the button has been pressed there is none, and every line is
        /// translated by engine.
        /// </summary>
        public string ReferenceTranslationsPath { get; set; } =
            "%APPDATA%/TataruHelper/ReferenceTranslations.db";

        /// <summary>
        /// Where earlier versions kept it, beside the application. Moved to
        /// <see cref="ReferenceTranslationsPath"/> once, so an installation
        /// that already has an index does not fetch it all over again.
        /// </summary>
        public string LegacyReferenceTranslationsPath { get; set; } = "Resources/ReferenceTranslations.db";

        /// <summary>
        /// The language to rebuild the index in, as the translation project
        /// names its files. Only Russian has a translation worth reading for
        /// now; an index already built says its own language, and this is what
        /// is used when there is no index yet to ask.
        /// </summary>
        public string ReferenceTranslationsLanguage { get; set; } = "ru";

        public string NTextCatLanguageModelsPath { get; set; } = "TranslationResources/Core14.profile.xml";

        public string PapagoEncoderPath { get; set; } = "TranslationResources/PapagoEncoder";

        public string PapagoKeyCachePath { get; set; } = "PapagoKey.cache";

        public string GoogleTranslateLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string PapagoLanguages { get; set; } = "TranslationResources/PapagoLanguages.json";

        public string AzureTranslatorLanguages { get; set; } = "TranslationResources/AzureTranslatorLanguages.json";

        public string GoogleCloudTranslateLanguages { get; set; } =
            "TranslationResources/GoogleTranslateLanguages.json";

        public string DeepLApiLanguages { get; set; } = "TranslationResources/DeeplLanguages.json";

        public string DeepLLanguages { get; set; } = "TranslationResources/DeeplLanguages.json";

        public string OpenAILanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string DeepSeekLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string YandexGptLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string GeminiLanguages { get; set; } = "TranslationResources/GoogleTranslateLanguages.json";

        public string YandexLanguages { get; set; } = "TranslationResources/YandexTranslateLanguages.json";

        public string YandexCloudLanguages { get; set; } = "TranslationResources/YandexCloudLanguages.json";

        public string YandexAuthFile { get; set; } = "TranslationResources/YandexAuth";

        public string YandexUsersFile { get; set; } = "TranslationResources/YandexUsers.json";

        public string YandexEncoderPath { get; set; } = "TranslationResources/YandexEncoder";
    }
}