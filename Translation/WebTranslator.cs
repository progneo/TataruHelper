// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Translation.Credentials;
using Translation.Exceptions;
using Translation.Providers;
using Translation.Utils;

namespace Translation
{
    public class WebTranslator
    {
        public ReadOnlyCollection<TranslationEngine> TranslationEngines
        {
            get { return _translationEngines; }
        }

        private ReadOnlyCollection<TranslationEngine> _translationEngines;

        private readonly List<KeyValuePair<TranslationRequest, string>> _translationCache;

        private readonly KeyValuePair<TranslationRequest, string> defaultCachedResult =
            default(KeyValuePair<TranslationRequest, string>);

        private readonly IReadOnlyDictionary<TranslationEngineName, ITranslationProvider> _TranslationProviders;

        private readonly LanguageDetector _LanguageDetector;
        private readonly Func<string, string> _detectLanguage;

        private readonly ILog _Logger;

        private readonly string _translationSettingsPath = "TranslationSysSettings.json";

        public WebTranslator(ILog logger)
            : this(logger, null, true, null, null)
        {
        }

        public WebTranslator(ILog logger, ITranslationCredentialStore credentials)
            : this(logger, null, true, null, credentials)
        {
        }

        public WebTranslator(ILog logger, IEnumerable<ITranslationProvider> translationProviders)
            : this(logger, translationProviders, true, null, null)
        {
        }

        internal WebTranslator(
            ILog logger,
            IEnumerable<ITranslationProvider> translationProviders,
            bool usePersistedSettings,
            Func<string, string> detectLanguage = null,
            ITranslationCredentialStore credentials = null)
        {
            _Logger = logger;

            if (usePersistedSettings &&
                !Helper.LoadStaticFromJson(typeof(GlobalTranslationSettings), _translationSettingsPath))
            {
                Helper.SaveStaticToJson(typeof(GlobalTranslationSettings), _translationSettingsPath);
                Helper.LoadStaticFromJson(typeof(GlobalTranslationSettings), _translationSettingsPath);
            }

            _translationCache =
                new List<KeyValuePair<TranslationRequest, string>>(GlobalTranslationSettings.TranslationCacheSize);

            _TranslationProviders = translationProviders != null
                ? translationProviders.ToDictionary(x => x.EngineName, x => x)
                : TranslationProviderFactory.CreateDefaultProviders(_Logger,
                    credentials ?? NullCredentialStore.Instance);

            _LanguageDetector = new LanguageDetector(GlobalTranslationSettings.MaxSameLanguagePercent,
                GlobalTranslationSettings.NTextCatLanguageModelsPath, _Logger);
            _detectLanguage = detectLanguage ?? _LanguageDetector.TryDetectLanguague;
        }

        public void LoadLanguages()
        {
            LoadLanguages(
                GlobalTranslationSettings.GoogleTranslateLanguages,
                GlobalTranslationSettings.PapagoLanguages,
                GlobalTranslationSettings.AzureTranslatorLanguages,
                GlobalTranslationSettings.GoogleCloudTranslateLanguages,
                GlobalTranslationSettings.DeepLApiLanguages,
                GlobalTranslationSettings.OpenAILanguages,
                GlobalTranslationSettings.DeepSeekLanguages,
                GlobalTranslationSettings.YandexCloudLanguages,
                GlobalTranslationSettings.YandexGptLanguages);
        }

        public Task<TranslationResult> TranslateAsync(string inSentence, TranslationEngine translationEngine,
            TranslatorLanguague fromLang, TranslatorLanguague toLang)
        {
            return TranslateAsync(inSentence, translationEngine, fromLang, toLang, CancellationToken.None);
        }

        public Task<TranslationResult> TranslateAsync(
            string inSentence,
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            TranslatorLanguague toLang,
            CancellationToken cancellationToken)
        {
            return TranslateCoreAsync(inSentence, translationEngine, fromLang, toLang, cancellationToken);
        }

        // Synchronous entry point kept for back-compat (and the characterization tests).
        // Production code calls TranslateAsync; the HttpClient-based providers run truly async.
        public TranslationResult Translate(string inSentence, TranslationEngine translationEngine,
            TranslatorLanguague fromLang, TranslatorLanguague toLang)
        {
            return TranslateCoreAsync(inSentence, translationEngine, fromLang, toLang, CancellationToken.None)
                .GetAwaiter().GetResult();
        }

        private async Task<TranslationResult> TranslateCoreAsync(string inSentence,
            TranslationEngine translationEngine, TranslatorLanguague fromLang, TranslatorLanguague toLang,
            CancellationToken cancellationToken)
        {
            if (translationEngine == null || fromLang == null || toLang == null)
            {
                return TranslationResult.Failure(
                    translationEngine?.EngineName ?? default,
                    TranslationFailureKind.ProviderUnavailable,
                    "Engine or language not specified.");
            }

            fromLang = ResolveSourceLanguage(translationEngine, fromLang, inSentence);

            if (fromLang.SystemName == toLang.SystemName)
                return TranslationResult.Success(translationEngine.EngineName, inSentence);

            if ((inSentence ?? string.Empty).All(x => !char.IsLetter(x)))
                return TranslationResult.Success(translationEngine.EngineName, inSentence);

            switch (toLang.SystemName)
            {
                case "Korean":
                    if (_LanguageDetector.HasKorean(inSentence))
                        return TranslationResult.Success(translationEngine.EngineName, inSentence);
                    break;
                case "Japanese":
                    if (_LanguageDetector.HasJapanese(inSentence))
                        return TranslationResult.Success(translationEngine.EngineName, inSentence);
                    break;
            }

            var normalizedSentence = PreprocessSentence(inSentence);
            var fromLangCode = fromLang.LanguageCode;
            var toLangCode = toLang.LanguageCode;

            var translationRequest =
                new TranslationRequest(normalizedSentence, translationEngine.EngineName, fromLangCode, toLangCode);
            var cachedResult = _translationCache.FirstOrDefault(x => x.Key == translationRequest);

            if (!cachedResult.Equals(defaultCachedResult))
            {
                return TranslationResult.Success(translationEngine.EngineName, cachedResult.Value);
            }

            var result = await InvokeSelectedProviderAsync(translationEngine.EngineName, normalizedSentence,
                fromLangCode, toLangCode, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Text))
            {
                cachedResult = _translationCache.FirstOrDefault(x => x.Key == translationRequest);
                if (cachedResult.Equals(defaultCachedResult))
                {
                    _translationCache.Add(
                        new KeyValuePair<TranslationRequest, string>(translationRequest, result.Text));
                }

                if (_translationCache.Count > GlobalTranslationSettings.TranslationCacheSize - 10)
                    _translationCache.RemoveRange(0, GlobalTranslationSettings.TranslationCacheSize / 2);
            }

            return result;
        }

        private async Task<TranslationResult> InvokeSelectedProviderAsync(
            TranslationEngineName engineName,
            string sentence,
            string fromLangCode,
            string toLangCode,
            CancellationToken cancellationToken)
        {
            if (!_TranslationProviders.TryGetValue(engineName, out var provider))
            {
                return TranslationResult.Failure(engineName, TranslationFailureKind.ProviderUnavailable,
                    "No provider registered for " + engineName);
            }

            try
            {
                var text = await provider.TranslateAsync(sentence, fromLangCode, toLangCode, cancellationToken)
                    .ConfigureAwait(false) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(text))
                {
                    return TranslationResult.Failure(engineName, TranslationFailureKind.EmptyResponse,
                        "Provider returned no translation.");
                }

                return TranslationResult.Success(engineName, text);
            }
            catch (QuotaExceededException quotaEx)
            {
                _Logger?.WriteLog("[PROVIDER_" + engineName + "_QUOTA] " + quotaEx.Message);
                return TranslationResult.Failure(engineName, TranslationFailureKind.QuotaExceeded, quotaEx.Message);
            }
            catch (MissingApiKeyException keyEx)
            {
                _Logger?.WriteLog("[PROVIDER_" + engineName + "_NO_KEY] " + keyEx.Message);
                return TranslationResult.Failure(engineName, TranslationFailureKind.MissingCredentials, keyEx.Message);
            }
            catch (Exception ex)
            {
                _Logger?.WriteLog("[PROVIDER_" + engineName + "_EXCEPTION] " + ex);
                return TranslationResult.Failure(engineName, TranslationFailureKind.ProviderException, ex.Message);
            }
        }

        private TranslatorLanguague ResolveSourceLanguage(
            TranslationEngine translationEngine,
            TranslatorLanguague fromLang,
            string sentence)
        {
            if (fromLang == null || fromLang.SystemName != "Auto")
                return fromLang;

            var detectedSystemLanguage = _detectLanguage(sentence ?? string.Empty);
            if (string.IsNullOrWhiteSpace(detectedSystemLanguage))
                return fromLang;

            var detectedLanguage = translationEngine.SupportedLanguages
                .FirstOrDefault(x => x.SystemName == detectedSystemLanguage);

            return detectedLanguage ?? fromLang;
        }

        private void LoadLanguages(
            string glTrPath,
            string PapagoTrPath,
            string azurePath,
            string gCloudPath,
            string deepLApiPath,
            string openAiPath,
            string deepSeekPath,
            string yandexCloudPath,
            string yandexGptPath)
        {
            try
            {
                List<TranslationEngine> tmptranslationEngines = new List<TranslationEngine>();
                var tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(glTrPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.GoogleTranslate, tmpList, 9));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(PapagoTrPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.Papago, tmpList, 6));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(azurePath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.AzureTranslator, tmpList, 9));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(gCloudPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.GoogleCloudTranslate, tmpList,
                    9));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(deepLApiPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.DeepLApi, tmpList, 10));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(openAiPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.OpenAI, tmpList, 8));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(deepSeekPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.DeepSeek, tmpList, 7));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(yandexCloudPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.Yandex, tmpList, 8));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(yandexGptPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.YandexGPT, tmpList, 8));

                tmptranslationEngines = tmptranslationEngines.OrderByDescending(x => x.Quality).ToList();


                _translationEngines = new ReadOnlyCollection<TranslationEngine>(tmptranslationEngines);
            }
            catch (Exception e)
            {
                _Logger.WriteLog(Convert.ToString(e));
            }
        }

        private string PreprocessSentence(string sentence)
        {
            return sentence ?? string.Empty;
        }
    }
}