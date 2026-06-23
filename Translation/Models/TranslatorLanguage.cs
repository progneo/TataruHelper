using System;

using Newtonsoft.Json;

namespace Translation.Models
{
    public class TranslatorLanguage : IEquatable<TranslatorLanguage>
    {
        [JsonProperty] public string ShownName { get; private set; }
        [JsonProperty] public string SystemName { get; private set; }
        [JsonProperty] public string LanguageCode { get; private set; }

        public TranslatorLanguage()
        {
            ShownName = String.Empty;
            SystemName = String.Empty;
            LanguageCode = String.Empty;
        }

        public TranslatorLanguage(string shownName, string systemName, string languageCode)
        {
            ShownName = shownName;
            SystemName = systemName;
            LanguageCode = languageCode;
        }

        public TranslatorLanguage(TranslatorLanguage language)
        {
            ShownName = language.ShownName;
            SystemName = language.SystemName;
            LanguageCode = language.LanguageCode;
        }

        public override bool Equals(object obj)
        {
            return this.Equals(obj as TranslatorLanguage);
        }

        public bool Equals(TranslatorLanguage lang)
        {
            if (ReferenceEquals(lang, null))
                return false;

            if (ReferenceEquals(this, lang))
                return true;

            if (this.GetType() != lang.GetType())
                return false;

            return this.SystemName == lang.SystemName;
        }

        public static bool operator ==(TranslatorLanguage left, TranslatorLanguage right)
        {
            if (ReferenceEquals(left, right))
                return true;

            if (ReferenceEquals(left, null))
                return false;

            if (ReferenceEquals(right, null))
                return false;

            return left.Equals(right);
        }

        public static bool operator !=(TranslatorLanguage left, TranslatorLanguage right) => !(left == right);

        public override int GetHashCode()
        {
            return SystemName.GetHashCode();
        }

        public override string ToString()
        {
            return $"Name: {ShownName ?? SystemName ?? "null"}; Code: {LanguageCode ?? "null"}";
        }
    }
}