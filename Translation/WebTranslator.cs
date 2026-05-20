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
            get { return _TranslationEngines; }
        }

        private ReadOnlyCollection<TranslationEngine> _TranslationEngines;

        private readonly List<KeyValuePair<TranslationRequest, string>> transaltionCache;

        private readonly KeyValuePair<TranslationRequest, string> defaultCachedResult =
            default(KeyValuePair<TranslationRequest, string>);

        private readonly IReadOnlyDictionary<TranslationEngineName, ITranslationProvider> _TranslationProviders;

        private readonly LanguageDetector _LanguageDetector;
        private readonly Func<string, string> _detectLanguage;

        private readonly ILog _Logger;

        private readonly string _TransaltionSettingsPath = "TranslationSysSettings.json";

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
                !Helper.LoadStaticFromJson(typeof(GlobalTranslationSettings), _TransaltionSettingsPath))
            {
                Helper.SaveStaticToJson(typeof(GlobalTranslationSettings), _TransaltionSettingsPath);
                Helper.LoadStaticFromJson(typeof(GlobalTranslationSettings), _TransaltionSettingsPath);
            }

            transaltionCache =
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
                GlobalTranslationSettings.DeeplLanguages,
                GlobalTranslationSettings.PapagoLanguages,
                GlobalTranslationSettings.BaiduLanguages,
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
            return Task.Run(() =>
            {
                cancellationToken.ThrowIfCancellationRequested();
                return Translate(inSentence, translationEngine, fromLang, toLang);
            }, cancellationToken);
        }

        public TranslationResult Translate(string inSentence, TranslationEngine translationEngine,
            TranslatorLanguague fromLang, TranslatorLanguague toLang)
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
            var cachedResult = transaltionCache.FirstOrDefault(x => x.Key == translationRequest);

            if (!cachedResult.Equals(defaultCachedResult))
            {
                return TranslationResult.Success(translationEngine.EngineName, cachedResult.Value);
            }

            var result = InvokeSelectedProvider(translationEngine.EngineName, normalizedSentence, fromLangCode,
                toLangCode);

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Text))
            {
                cachedResult = transaltionCache.FirstOrDefault(x => x.Key == translationRequest);
                if (cachedResult.Equals(defaultCachedResult))
                {
                    transaltionCache.Add(
                        new KeyValuePair<TranslationRequest, string>(translationRequest, result.Text));
                }

                if (transaltionCache.Count > GlobalTranslationSettings.TranslationCacheSize - 10)
                    transaltionCache.RemoveRange(0, GlobalTranslationSettings.TranslationCacheSize / 2);
            }

            return result;
        }

        private TranslationResult InvokeSelectedProvider(
            TranslationEngineName engineName,
            string sentence,
            string fromLangCode,
            string toLangCode)
        {
            if (!_TranslationProviders.TryGetValue(engineName, out var provider))
            {
                return TranslationResult.Failure(engineName, TranslationFailureKind.ProviderUnavailable,
                    "No provider registered for " + engineName);
            }

            try
            {
                var text = provider.Translate(sentence, fromLangCode, toLangCode) ?? string.Empty;

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
            string deepPath,
            string PapagoTrPath,
            string baiduTrPath,
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

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(deepPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.DeepL, tmpList, 10));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(PapagoTrPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.Papago, tmpList, 6));

                tmpList = Helper.LoadJsonData<List<TranslatorLanguague>>(baiduTrPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.Baidu, tmpList, 3));

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


                _TranslationEngines = new ReadOnlyCollection<TranslationEngine>(tmptranslationEngines);
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