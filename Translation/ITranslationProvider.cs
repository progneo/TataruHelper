using System.Threading;
using System.Threading.Tasks;

namespace Translation
{
    public interface ITranslationProvider
    {
        TranslationEngineName EngineName { get; }

        string Translate(string sentence, string inLang, string outLang);

        // Asynchronous translation. Providers backed by HttpClient override this with a
        // genuinely non-blocking implementation; the default bridges to the synchronous
        // Translate on a thread-pool thread so existing sync-only providers (and test
        // doubles) keep working unchanged.
        Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            return Task.Run(() => Translate(sentence, inLang, outLang), cancellationToken);
        }
    }
}
