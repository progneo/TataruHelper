using Translation.Models;

namespace Translation.Credentials
{
    public sealed class NullCredentialStore : ITranslationCredentialStore
    {
        public static readonly NullCredentialStore Instance = new NullCredentialStore();

        public string GetApiKey(TranslationEngineName engine) => string.Empty;

        public string GetRegion(TranslationEngineName engine) => string.Empty;

        public string GetModel(TranslationEngineName engine) => string.Empty;

        public bool IsEngineEnabled(TranslationEngineName engine) => true;

        public void SetApiKey(TranslationEngineName engine, string apiKey) { }

        public void SetRegion(TranslationEngineName engine, string region) { }

        public void SetModel(TranslationEngineName engine, string model) { }

        public void SetEngineEnabled(TranslationEngineName engine, bool isEnabled) { }

        public void Save() { }
    }
}