using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Translation.Credentials;
using Translation.Models;
using Translation.Providers.AI;

namespace Translation.Providers.Gemini
{
    internal sealed class GeminiTranslator : ITranslationProvider
    {
        public TranslationEngineName EngineName => TranslationEngineName.Gemini;

        private readonly GeminiChatClient _client;

        public GeminiTranslator(ILogger logger, ITranslationCredentialStore credentials)
        {
            _client = new GeminiChatClient(
                TranslationEngineName.Gemini,
                // Flash rather than Pro: dialogue arrives line by line, so latency
                // matters more here than the extra quality on long documents.
                "gemini-2.0-flash",
                logger,
                credentials);
        }

        public Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            return _client.TranslateAsync(sentence, inLang, outLang, cancellationToken);
        }
    }
}
