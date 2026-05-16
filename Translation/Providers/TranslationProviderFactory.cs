using System.Collections.Generic;
using System.Linq;
using Translation.Baidu;
using Translation.Deepl;
using Translation.Google;
using Translation.Papago;

namespace Translation.Providers
{
    internal static class TranslationProviderFactory
    {
        public static IReadOnlyDictionary<TranslationEngineName, ITranslationProvider> CreateDefaultProviders(ILog logger)
        {
            var providers = new ITranslationProvider[]
            {
                new TranslationProviderAdapter(TranslationEngineName.GoogleTranslate, new GoogleTranslator(logger).Translate),
                new TranslationProviderAdapter(TranslationEngineName.DeepL, new DeepLTranslator(logger).Translate),
                new TranslationProviderAdapter(TranslationEngineName.Papago, new PapagoTranslator(logger).Translate),
                new TranslationProviderAdapter(TranslationEngineName.Baidu, new BaiduTranslater(logger).Translate),
            };

            return providers.ToDictionary(x => x.EngineName, x => x);
        }
    }
}
