using System.Threading;
using System.Threading.Tasks;

using Translation.Credentials;

namespace Translation.AI
{
    internal sealed class DeepSeekTranslator
    {
        private readonly OpenAIChatClient _client;

        public DeepSeekTranslator(ILog logger, ITranslationCredentialStore credentials)
        {
            _client = new OpenAIChatClient(
                TranslationEngineName.DeepSeek,
                "https://api.deepseek.com/chat/completions",
                "deepseek-chat",
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