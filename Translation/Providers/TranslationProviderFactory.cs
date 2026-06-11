using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Translation.Credentials;
using Translation.Models;
using Translation.Providers.Azure;
using Translation.Providers.DeepL;
using Translation.Providers.DeepSeek;
using Translation.Providers.Google;
using Translation.Providers.GoogleCloud;
using Translation.Providers.OpenAI;
using Translation.Providers.Papago;
using Translation.Providers.YandexCloud;
using Translation.Providers.YandexGpt;
using Translation.Settings;

namespace Translation.Providers
{
    internal static class TranslationProviderFactory
    {
        public static IReadOnlyDictionary<TranslationEngineName, ITranslationProvider> CreateDefaultProviders(
            ILogger logger,
            ITranslationCredentialStore credentials,
            TranslationSettings settings)
        {
            credentials = credentials ?? NullCredentialStore.Instance;
            settings = settings ?? new TranslationSettings();

            var google = new GoogleTranslator(logger, settings);
            var papago = new PapagoTranslator(logger, settings);
            var deepLF = new DeepLTranslator(logger, settings);
            var azure = new AzureTranslator(logger, credentials);
            var googleCloud = new GoogleCloudTranslator(logger, credentials);
            var deepLApi = new DeepLApiTranslator(logger, credentials);
            var openAi = new OpenAITranslator(logger, credentials);
            var deepSeek = new DeepSeekTranslator(logger, credentials);
            var yandexCloud = new YandexCloudTranslator(logger, credentials);
            var yandexGpt = new YandexGptTranslator(logger, credentials);

            var providers = new ITranslationProvider[]
            {
                google, papago, deepLF, azure, googleCloud, deepLApi, openAi, deepSeek, yandexCloud, yandexGpt,
            };

            return providers.ToDictionary(x => x.EngineName, x => x);
        }
    }
}