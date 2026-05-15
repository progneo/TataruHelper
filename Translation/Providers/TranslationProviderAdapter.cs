using System;

namespace Translation.Providers
{
    internal sealed class TranslationProviderAdapter : ITranslationProvider
    {
        private readonly Func<string, string, string, string> _translateFunc;

        public TranslationEngineName EngineName { get; private set; }

        public TranslationProviderAdapter(TranslationEngineName engineName, Func<string, string, string, string> translateFunc)
        {
            EngineName = engineName;
            _translateFunc = translateFunc;
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            if (_translateFunc == null)
                return String.Empty;

            return _translateFunc(sentence, inLang, outLang);
        }
    }
}
