using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Translation.Credentials;
using Translation.Models;
using Translation.Providers.AI;

namespace Translation.Providers.Claude
{
    internal sealed class ClaudeTranslator : ITranslationProvider
    {
        public TranslationEngineName EngineName => TranslationEngineName.Claude;

        private readonly ClaudeChatClient _client;

        public ClaudeTranslator(ILogger logger, ITranslationCredentialStore credentials)
        {
            _client = new ClaudeChatClient(
                TranslationEngineName.Claude,
                "claude-opus-5",
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
