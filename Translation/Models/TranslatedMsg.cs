using System;

namespace Translation.Models
{
    public class TranslatedMsg
    {
        public string OriginalText;
        public string TranslatedText;

        public bool IsTranslated
        {
            get
            {
                if (TranslatedText == String.Empty)
                    return true;

                return false;
            }
        }

        public TranslatedMsg(string originalText, string translatedText)
        {
            OriginalText = originalText;
            translatedText = TranslatedText;
        }

        public TranslatedMsg()
        {
            OriginalText = String.Empty;
            TranslatedText = String.Empty;
        }
    }
}