using System.Threading;
using System.Threading.Tasks;

using Translation.Models;

namespace Translation
{
    public interface ITranslationProvider
    {
        TranslationEngineName EngineName { get; }

        Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken);
    }
}