using Translation.Core;

namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public readonly struct OutgoingChatResult
    {
        public OutgoingChatResultKind Kind { get; }

        public string ClipboardPayload { get; }

        public TranslationResult Translation { get; }

        public string ErrorMessage { get; }

        private OutgoingChatResult(
            OutgoingChatResultKind kind,
            string clipboardPayload,
            TranslationResult translation,
            string errorMessage)
        {
            Kind = kind;
            ClipboardPayload = clipboardPayload ?? string.Empty;
            Translation = translation;
            ErrorMessage = errorMessage ?? string.Empty;
        }

        public bool IsSuccess => Kind == OutgoingChatResultKind.Success;

        public static OutgoingChatResult Success(string payload, TranslationResult translation)
            => new OutgoingChatResult(OutgoingChatResultKind.Success, payload, translation, null);

        public static OutgoingChatResult Failure(OutgoingChatResultKind kind, string errorMessage,
            TranslationResult translation = default)
            => new OutgoingChatResult(kind, null, translation, errorMessage);
    }
}