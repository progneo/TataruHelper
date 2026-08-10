using Translation.Models;

namespace Translation.Credentials
{
    public interface ITranslationCredentialStore
    {
        string GetApiKey(TranslationEngineName engine);

        string GetRegion(TranslationEngineName engine);

        string GetModel(TranslationEngineName engine);

        /// <summary>
        /// Where to send the request, for engines that do not have one fixed
        /// address: a local server on whichever port its owner chose, or a
        /// self-hosted instance. Empty means the provider's own default.
        /// </summary>
        string GetEndpoint(TranslationEngineName engine);

        bool IsEngineEnabled(TranslationEngineName engine);

        void SetApiKey(TranslationEngineName engine, string apiKey);

        void SetRegion(TranslationEngineName engine, string region);

        void SetModel(TranslationEngineName engine, string model);

        void SetEndpoint(TranslationEngineName engine, string endpoint);

        void SetEngineEnabled(TranslationEngineName engine, bool isEnabled);

        void Save();
    }
}