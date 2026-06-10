using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Translation.Credentials;
using Translation.Exceptions;
using Translation.Http;
using Translation.Models;
using Translation.Providers;
using Translation.Settings;
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
        private readonly object _cacheSync = new object();

        private readonly KeyValuePair<TranslationRequest, string> defaultCachedResult =
            default(KeyValuePair<TranslationRequest, string>);

        private readonly IReadOnlyDictionary<TranslationEngineName, ITranslationProvider> _TranslationProviders;

        private readonly LanguageDetector _LanguageDetector;
        private readonly Func<string, string> _detectLanguage;

        private readonly ILogger _Logger;
        private readonly TranslationSettings _settings;

        private readonly string _translationSettingsPath = "TranslationSysSettings.json";

        public WebTranslator(ILogger logger)
            : this(logger, null, null, null, null)
        {
        }

        public WebTranslator(ILogger logger, ITranslationCredentialStore credentials)
            : this(logger, null, null, null, credentials)
        {
        }

        public WebTranslator(ILogger logger, IEnumerable<ITranslationProvider> translationProviders)
            : this(logger, translationProviders, null, null, null)
        {
        }

        internal WebTranslator(
            ILogger logger,
            IEnumerable<ITranslationProvider> translationProviders,
            TranslationSettings settings,
            Func<string, string> detectLanguage = null,
            ITranslationCredentialStore credentials = null)
        {
            _Logger = logger;

            if (settings == null)
            {
                settings = TranslationSettingsStorage.Load(_translationSettingsPath, _Logger);
                if (settings == null)
                {
                    settings = new TranslationSettings();
                    TranslationSettingsStorage.Save(settings, _translationSettingsPath, _Logger);
                }
            }

            _settings = settings;
            ApiHttpClient.Configure(_settings.HttpRequestTimeoutMilliseconds,
                _settings.HttpReadWriteTimeoutMilliseconds);

            _translationCache =
                new List<KeyValuePair<TranslationRequest, string>>(_settings.TranslationCacheSize);

            _TranslationProviders = translationProviders != null
                ? translationProviders.ToDictionary(x => x.EngineName, x => x)
                : TranslationProviderFactory.CreateDefaultProviders(_Logger,
                    credentials ?? NullCredentialStore.Instance, _settings);

            _LanguageDetector = new LanguageDetector(_settings.MaxSameLanguagePercent,
                _settings.NTextCatLanguageModelsPath, _Logger);
            _detectLanguage = detectLanguage ?? _LanguageDetector.TryDetectLanguage;
        }

        public void LoadLanguages()
        {
            LoadLanguages(
                _settings.GoogleTranslateLanguages,
                _settings.PapagoLanguages,
                _settings.AzureTranslatorLanguages,
                _settings.GoogleCloudTranslateLanguages,
                _settings.DeepLApiLanguages,
                _settings.OpenAILanguages,
                _settings.DeepSeekLanguages,
                _settings.YandexCloudLanguages,
                _settings.YandexGptLanguages);
        }

        public Task<TranslationResult> TranslateAsync(string inSentence, TranslationEngine translationEngine,
            TranslatorLanguage fromLang, TranslatorLanguage toLang)
        {
            return TranslateAsync(inSentence, translationEngine, fromLang, toLang, CancellationToken.None);
        }

        public Task<TranslationResult> TranslateAsync(
            string inSentence,
            TranslationEngine translationEngine,
            TranslatorLanguage fromLang,
            TranslatorLanguage toLang,
            CancellationToken cancellationToken)
        {
            return TranslateCoreAsync(inSentence, translationEngine, fromLang, toLang, cancellationToken);
        }

        private async Task<TranslationResult> TranslateCoreAsync(string inSentence,
            TranslationEngine translationEngine, TranslatorLanguage fromLang, TranslatorLanguage toLang,
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
            KeyValuePair<TranslationRequest, string> cachedResult;
            lock (_cacheSync)
            {
                cachedResult = _translationCache.FirstOrDefault(x => x.Key == translationRequest);
            }

            if (!cachedResult.Equals(defaultCachedResult))
            {
                return TranslationResult.Success(translationEngine.EngineName, cachedResult.Value);
            }

            var result = await InvokeSelectedProviderAsync(translationEngine.EngineName, normalizedSentence,
                fromLangCode, toLangCode, cancellationToken).ConfigureAwait(false);

            if (result.IsSuccess && !string.IsNullOrEmpty(result.Text))
            {
                lock (_cacheSync)
                {
                    cachedResult = _translationCache.FirstOrDefault(x => x.Key == translationRequest);
                    if (cachedResult.Equals(defaultCachedResult))
                    {
                        _translationCache.Add(
                            new KeyValuePair<TranslationRequest, string>(translationRequest, result.Text));
                    }

                    if (_translationCache.Count > _settings.TranslationCacheSize - 10)
                        _translationCache.RemoveRange(0, _settings.TranslationCacheSize / 2);
                }
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
                _Logger?.LogInformation("{Message}", "[PROVIDER_" + engineName + "_QUOTA] " + quotaEx.Message);
                return TranslationResult.Failure(engineName, TranslationFailureKind.QuotaExceeded, quotaEx.Message);
            }
            catch (MissingApiKeyException keyEx)
            {
                _Logger?.LogInformation("{Message}", "[PROVIDER_" + engineName + "_NO_KEY] " + keyEx.Message);
                return TranslationResult.Failure(engineName, TranslationFailureKind.MissingCredentials, keyEx.Message);
            }
            catch (Exception ex)
            {
                _Logger?.LogInformation("{Message}", "[PROVIDER_" + engineName + "_EXCEPTION] " + ex);
                return TranslationResult.Failure(engineName, TranslationFailureKind.ProviderException, ex.Message);
            }
        }

        private TranslatorLanguage ResolveSourceLanguage(
            TranslationEngine translationEngine,
            TranslatorLanguage fromLang,
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
                var tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(glTrPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.GoogleTranslate, tmpList, 9));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(PapagoTrPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.Papago, tmpList, 6));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(azurePath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.AzureTranslator, tmpList, 9));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(gCloudPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.GoogleCloudTranslate, tmpList,
                    9));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(deepLApiPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.DeepLApi, tmpList, 10));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(openAiPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.OpenAI, tmpList, 8));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(deepSeekPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.DeepSeek, tmpList, 7));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(yandexCloudPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.Yandex, tmpList, 8));

                tmpList = JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(yandexGptPath, _Logger);
                tmptranslationEngines.Add(new TranslationEngine(TranslationEngineName.YandexGPT, tmpList, 8));

                tmptranslationEngines = tmptranslationEngines.OrderByDescending(x => x.Quality).ToList();


                _translationEngines = new ReadOnlyCollection<TranslationEngine>(tmptranslationEngines);
            }
            catch (Exception e)
            {
                _Logger.LogInformation("{Message}", Convert.ToString(e));
            }
        }

        private string PreprocessSentence(string sentence)
        {
            return sentence ?? string.Empty;
        }
    }
}