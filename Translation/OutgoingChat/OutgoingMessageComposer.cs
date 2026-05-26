using System;

namespace Translation.OutgoingChat
{
    public sealed class OutgoingMessageComposer
    {
        private readonly IMessageSanitizer _sanitizer;
        private readonly IChannelPrefixFormatter _prefixFormatter;

        public OutgoingMessageComposer(IMessageSanitizer sanitizer, IChannelPrefixFormatter prefixFormatter)
        {
            _sanitizer = sanitizer ?? throw new ArgumentNullException(nameof(sanitizer));
            _prefixFormatter = prefixFormatter ?? throw new ArgumentNullException(nameof(prefixFormatter));
        }

        public string Compose(
            string translatedText,
            string originalText,
            ChatChannel channel,
            string tellTarget,
            OutgoingMessageComposeOptions options)
        {
            options = options ?? new OutgoingMessageComposeOptions();

            var body = _sanitizer.Sanitize(translatedText);

            if (options.AppendOriginalInParentheses)
            {
                var sanitizedOriginal = _sanitizer.Sanitize(originalText);
                if (sanitizedOriginal.Length > 0 && sanitizedOriginal != body)
                {
                    body = body.Length == 0
                        ? "(" + sanitizedOriginal + ")"
                        : body + " (" + sanitizedOriginal + ")";
                }
            }

            if (!options.PrependChannelCommand)
            {
                return body;
            }

            var prefix = _prefixFormatter.FormatPrefix(channel, tellTarget);
            return prefix + body;
        }
    }
}