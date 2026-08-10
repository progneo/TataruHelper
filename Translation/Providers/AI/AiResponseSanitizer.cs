using System;

namespace Translation.Providers.AI
{
    internal static class AiResponseSanitizer
    {
        /// <summary>
        /// Tags a model sometimes writes into its visible answer when it was
        /// told not to reason separately.
        ///
        /// Deliberately a short list rather than "anything in angle brackets":
        /// FFXIV chat is full of real markup - emote tags like &lt;/salute&gt;,
        /// auto-translate markers - and the translation is supposed to keep it.
        /// </summary>
        private static readonly string[] InternalTags = { "thinking", "reasoning", "scratchpad" };

        public static string StripWrappingArtifacts(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var trimmed = StripLeadingInternalBlock(text.Trim());

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = trimmed.IndexOf('\n');
                if (firstNewline > 0)
                {
                    trimmed = trimmed[(firstNewline + 1)..];
                }

                if (trimmed.EndsWith("```", StringComparison.Ordinal))
                {
                    trimmed = trimmed[..^3];
                }

                trimmed = trimmed.Trim();
            }

            if (trimmed.Length >= 2 &&
                ((trimmed[0] == '"' && trimmed[^1] == '"') ||
                 (trimmed[0] == '\'' && trimmed[^1] == '\'') ||
                 (trimmed[0] == '“' && trimmed[^1] == '”')))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            return trimmed;
        }

        /// <summary>
        /// Drops a leading block of the model's own reasoning, but only when
        /// something is left after it - if the whole reply is that block, there
        /// is no translation to show and an empty string is the honest answer,
        /// which the caller already treats as a failure.
        /// </summary>
        private static string StripLeadingInternalBlock(string text)
        {
            foreach (var tag in InternalTags)
            {
                var open = "<" + tag + ">";
                var close = "</" + tag + ">";

                if (!text.StartsWith(open, StringComparison.OrdinalIgnoreCase))
                    continue;

                var end = text.IndexOf(close, StringComparison.OrdinalIgnoreCase);
                if (end < 0)
                    continue;

                var rest = text[(end + close.Length)..].Trim();
                if (rest.Length > 0)
                    return rest;
            }

            return text;
        }
    }
}