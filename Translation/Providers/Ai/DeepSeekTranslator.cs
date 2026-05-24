using Translation.Credentials;

namespace Translation.Providers.Ai
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
    }
}