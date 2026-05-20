using System;

namespace Translation.AI
{
    internal static class AiResponseSanitizer
    {
        public static string StripWrappingArtifacts(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var trimmed = text.Trim();

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
    }
}