namespace Translation.Abstractions
{
    public interface ITranslationProvider
    {
        TranslationEngineName EngineName { get; }

        string Translate(string sentence, string inLang, string outLang);
    }
}