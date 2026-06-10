using System;

namespace Translation.Models
{
    struct TranslationRequest : IEquatable<TranslationRequest>
    {
        public string InSentence { get; private set; }
        public TranslationEngineName TranslationEngineName { get; private set; }
        public string FromLang { get; private set; }
        public string ToLang { get; private set; }

        public TranslationRequest(string inSentence, TranslationEngineName translationEngineName, string fromLang,
            string toLang)
        {
            InSentence = inSentence;
            TranslationEngineName = translationEngineName;
            FromLang = fromLang;
            ToLang = toLang;
        }

        public override bool Equals(object obj)
        {
            if (obj is TranslationRequest)
                return this.Equals((TranslationRequest)obj);

            return false;
        }

        public bool Equals(TranslationRequest reqv)
        {
            bool result = InSentence == reqv.InSentence && TranslationEngineName == reqv.TranslationEngineName;
            result = result && FromLang == reqv.FromLang && ToLang == reqv.ToLang;

            return result;
        }

        public static bool operator ==(TranslationRequest left, TranslationRequest right) => left.Equals(right);

        public static bool operator !=(TranslationRequest left, TranslationRequest right) => !(left == right);

        public override int GetHashCode()
        {
            return HashCode.Combine(InSentence, TranslationEngineName, FromLang, ToLang);
        }
    }
}