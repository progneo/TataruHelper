namespace Translation
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

        private TranslationResult(
            bool isSuccess,
            string text,
            TranslationEngineName engine,
            TranslationFailureKind failureKind,
            string failureReason)
        {
            IsSuccess = isSuccess;
            Text = text ?? string.Empty;
            Engine = engine;
            FailureKind = failureKind;
            FailureReason = failureReason ?? string.Empty;
        }

        public static TranslationResult Success(TranslationEngineName engine, string text)
            => new TranslationResult(true, text, engine, TranslationFailureKind.None, null);

        public static TranslationResult Failure(
            TranslationEngineName engine,
            TranslationFailureKind kind,
            string reason)
            => new TranslationResult(false, string.Empty, engine, kind, reason);
    }
}