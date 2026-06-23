using Translation.Models;

namespace Translation.Credentials
{
    public interface ITranslationCredentialStore
    {
        string GetApiKey(TranslationEngineName engine);

        string GetRegion(TranslationEngineName engine);

        string GetModel(TranslationEngineName engine);

        bool IsEngineEnabled(TranslationEngineName engine);

        void SetApiKey(TranslationEngineName engine, string apiKey);

        void SetRegion(TranslationEngineName engine, string region);

        void SetModel(TranslationEngineName engine, string model);

        void SetEngineEnabled(TranslationEngineName engine, bool isEnabled);

        void Save();
    }
}