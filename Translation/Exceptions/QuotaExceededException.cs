using System;

namespace Translation.Exceptions
{
    public class QuotaExceededException : Exception
    {
        public TranslationEngineName EngineName { get; }

        public QuotaExceededException(TranslationEngineName engineName, string message)
            : base(message)
        {
            EngineName = engineName;
        }

        public QuotaExceededException(TranslationEngineName engineName, string message, Exception innerException)
            : base(message, innerException)
        {
            EngineName = engineName;
        }
    }
}