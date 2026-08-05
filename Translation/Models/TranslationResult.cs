namespace Translation.Models
{
    public enum TranslationFailureKind
    {
        None,
        ProviderUnavailable,
        MissingCredentials,
        QuotaExceeded,
        ProviderException,
        EmptyResponse
    }

    public readonly struct TranslationResult
    {
        public bool IsSuccess { get; }
        public string Text { get; }
        public TranslationEngineName Engine { get; }
        public TranslationFailureKind FailureKind { get; }
        public string FailureReason { get; }

        /// <summary>
        /// Whether this came from a translation somebody made by hand rather
        /// than from an engine. Nothing about the text says which, and the
        /// difference is worth being able to see.
        /// </summary>
        public bool IsLiterary { get; }

        private TranslationResult(
            bool isSuccess,
            string text,
            TranslationEngineName engine,
            TranslationFailureKind failureKind,
            string failureReason,
            bool isLiterary = false)
        {
            IsSuccess = isSuccess;
            Text = text ?? string.Empty;
            Engine = engine;
            FailureKind = failureKind;
            FailureReason = failureReason ?? string.Empty;
            IsLiterary = isLiterary;
        }

        public static TranslationResult Success(TranslationEngineName engine, string text)
            => new TranslationResult(true, text, engine, TranslationFailureKind.None, null);

        public static TranslationResult Literary(TranslationEngineName engine, string text)
            => new TranslationResult(true, text, engine, TranslationFailureKind.None, null, true);

        /// <summary>
        /// The same result reading differently - a speaker prefixed, a marker
        /// added. Where it came from does not change, and rebuilding it with
        /// Success would quietly claim an engine had produced it.
        /// </summary>
        public TranslationResult WithText(string text)
            => new TranslationResult(IsSuccess, text, Engine, FailureKind, FailureReason, IsLiterary);

        public static TranslationResult Failure(
            TranslationEngineName engine,
            TranslationFailureKind kind,
            string reason)
            => new TranslationResult(false, string.Empty, engine, kind, reason);
    }
}