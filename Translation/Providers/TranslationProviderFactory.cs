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

            var providers = new ITranslationProvider[]
            {
                new TranslationProviderAdapter(TranslationEngineName.GoogleTranslate,
                    new GoogleTranslator(logger).Translate),
                new TranslationProviderAdapter(TranslationEngineName.Papago,
                    new PapagoTranslator(logger).Translate),
                new TranslationProviderAdapter(TranslationEngineName.AzureTranslator,
                    new AzureTranslator.AzureTranslator(logger, credentials).Translate),
                new TranslationProviderAdapter(TranslationEngineName.GoogleCloudTranslate,
                    new GoogleCloudTranslator(logger, credentials).Translate),
                new TranslationProviderAdapter(TranslationEngineName.DeepLApi,
                    new DeepLApiTranslator(logger, credentials).Translate),
                new TranslationProviderAdapter(TranslationEngineName.OpenAI,
                    new OpenAITranslator(logger, credentials).Translate),
                new TranslationProviderAdapter(TranslationEngineName.DeepSeek,
                    new DeepSeekTranslator(logger, credentials).Translate),
                new TranslationProviderAdapter(TranslationEngineName.Yandex,
                    new YandexCloudTranslator(logger, credentials).Translate),
                new TranslationProviderAdapter(TranslationEngineName.YandexGPT,
                    new YandexGptTranslator(logger, credentials).Translate),
            };

            return providers.ToDictionary(x => x.EngineName, x => x);
        }
    }
}