using System;
using System.Threading;
using System.Threading.Tasks;

namespace Translation.Providers
{
    internal sealed class TranslationProviderAdapter : ITranslationProvider
    {
        private readonly Func<string, string, string, string> _translateFunc;
        private readonly Func<string, string, string, CancellationToken, Task<string>> _translateAsyncFunc;

        public TranslationEngineName EngineName { get; private set; }

        public TranslationProviderAdapter(
            TranslationEngineName engineName,
            Func<string, string, string, string> translateFunc,
            Func<string, string, string, CancellationToken, Task<string>> translateAsyncFunc = null)
        {
            EngineName = engineName;
            _translateFunc = translateFunc;
            _translateAsyncFunc = translateAsyncFunc;
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            if (_translateFunc == null)
                return String.Empty;

            return _translateFunc(sentence, inLang, outLang);
        }

        public Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            if (_translateAsyncFunc != null)
                return _translateAsyncFunc(sentence, inLang, outLang, cancellationToken);

            return Task.Run(() => Translate(sentence, inLang, outLang), cancellationToken);
        }
    }
}
