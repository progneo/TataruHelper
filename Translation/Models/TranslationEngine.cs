using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace Translation.Models
{
    public enum TranslationEngineName : int
    {
        GoogleTranslate = 0,
        Yandex = 3,
        Papago = 5,
        AzureTranslator = 7,
        GoogleCloudTranslate = 8,
        DeepLApi = 9,
        OpenAI = 10,
        DeepSeek = 11,
        YandexGPT = 12,
        DeepL = 13,
    }

    public class TranslationEngine : IEquatable<TranslationEngine>
    {
        public string Name
        {
            get { return EngineName.ToString(); }
        }

        public ReadOnlyCollection<TranslatorLanguage> SupportedLanguages
        {
            get { return _SupportedLanguages; }
        }

        public TranslationEngineName EngineName { get; private set; }
        public double Quality { get; private set; }

        ReadOnlyCollection<TranslatorLanguage> _SupportedLanguages;

        public TranslationEngine(TranslationEngineName translationEngineName,
            List<TranslatorLanguage> translatorLanguages, double quality)
        {
            this.EngineName = translationEngineName;
            _SupportedLanguages = new ReadOnlyCollection<TranslatorLanguage>(translatorLanguages);
            Quality = quality;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TranslationEngine);
        }

        public bool Equals(TranslationEngine engine)
        {
            if (ReferenceEquals(engine, null))
                return false;

            if (ReferenceEquals(this, engine))
                return true;

            if (this.GetType() != engine.GetType())
                return false;

            return this.EngineName == engine.EngineName;
        }

        public static bool operator ==(TranslationEngine left, TranslationEngine right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (ReferenceEquals(left, null))
                return false;

            if (ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TranslationEngine left, TranslationEngine right) => !(left == right);

        public override int GetHashCode()
        {
            return ((int)EngineName).GetHashCode();
        }
    }
}