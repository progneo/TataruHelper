using System.Threading;
using System.Threading.Tasks;

using Translation.Credentials;

namespace Translation.AI
{
    internal sealed class OpenAITranslator
    {
        private readonly OpenAIChatClient _client;

        public OpenAITranslator(ILog logger, ITranslationCredentialStore credentials)
        {
            _client = new OpenAIChatClient(
                TranslationEngineName.OpenAI,
                "https://api.openai.com/v1/chat/completions",
                "gpt-4o-mini",
                logger,
                credentials);
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            return _client.Translate(sentence, inLang, outLang);
        }

        public Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            return _client.TranslateAsync(sentence, inLang, outLang, cancellationToken);
        }
    }
}