using System;

using Translation.Models;

namespace Translation.Exceptions
{
    public class MissingApiKeyException : Exception
    {
        public TranslationEngineName EngineName { get; }

        public MissingApiKeyException(TranslationEngineName engineName)
            : base($"API key for engine '{engineName}' is not configured.")
        {
            EngineName = engineName;
        }
    }
}