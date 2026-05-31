using System.Collections.Generic;
using System.Linq;

using Translation.AI;
using Translation.Credentials;
using Translation.DeepLApi;
using Translation.Google;
using Translation.GoogleCloud;
using Translation.Papago;
using Translation.YandexCloud;

namespace Translation.Providers
{
    internal static class TranslationProviderFactory
    {
        public static IReadOnlyDictionary<TranslationEngineName, ITranslationProvider> CreateDefaultProviders(
            ILog logger,
            ITranslationCredentialStore credentials)
        {
            credentials = credentials ?? NullCredentialStore.Instance;

            // GoogleTranslate and Papago still use the legacy HttpReader (WebRequest) path and
            // expose only a synchronous Translate; the adapter bridges them to async via
            // Task.Run. The HttpClient-based engines below provide a genuinely async path.
            var google = new GoogleTranslator(logger);
            var papago = new PapagoTranslator(logger);
            var azure = new AzureTranslator.AzureTranslator(logger, credentials);
            var googleCloud = new GoogleCloudTranslator(logger, credentials);
            var deepL = new DeepLApiTranslator(logger, credentials);
            var openAi = new OpenAITranslator(logger, credentials);
            var deepSeek = new DeepSeekTranslator(logger, credentials);
            var yandexCloud = new YandexCloudTranslator(logger, credentials);
            var yandexGpt = new YandexGptTranslator(logger, credentials);

            var providers = new ITranslationProvider[]
            {
                new TranslationProviderAdapter(TranslationEngineName.GoogleTranslate, google.Translate),
                new TranslationProviderAdapter(TranslationEngineName.Papago, papago.Translate),
                new TranslationProviderAdapter(TranslationEngineName.AzureTranslator,
                    azure.Translate, azure.TranslateAsync),
                new TranslationProviderAdapter(TranslationEngineName.GoogleCloudTranslate,
                    googleCloud.Translate, googleCloud.TranslateAsync),
                new TranslationProviderAdapter(TranslationEngineName.DeepLApi,
                    deepL.Translate, deepL.TranslateAsync),
                new TranslationProviderAdapter(TranslationEngineName.OpenAI,
                    openAi.Translate, openAi.TranslateAsync),
                new TranslationProviderAdapter(TranslationEngineName.DeepSeek,
                    deepSeek.Translate, deepSeek.TranslateAsync),
                new TranslationProviderAdapter(TranslationEngineName.Yandex,
                    yandexCloud.Translate, yandexCloud.TranslateAsync),
                new TranslationProviderAdapter(TranslationEngineName.YandexGPT,
                    yandexGpt.Translate, yandexGpt.TranslateAsync),
            };

            return providers.ToDictionary(x => x.EngineName, x => x);
        }
    }
}