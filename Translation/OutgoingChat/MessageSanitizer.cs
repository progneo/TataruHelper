using System.Linq;
using System.Text;

namespace Translation.OutgoingChat
{
    public sealed class MessageSanitizer : IMessageSanitizer
    {
        public string Sanitize(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }

            var builder = new StringBuilder(text.Length);
            foreach (var ch in text.Where(IsAllowed))
            {
                builder.Append(ch);
            }

            return builder.ToString();
        }

        public int Utf8ByteLength(string text)
        {
            return string.IsNullOrEmpty(text) ? 0 : Encoding.UTF8.GetByteCount(text);
        }

        private static bool IsAllowed(char ch)
        {
            if (ch == '\t' || ch == '\n' || ch == '\r')
            {
                return true;
            }

            if (ch < 0x20)
            {
                return false;
            }

            if (ch == 0x7F)
            {
                return false;
            }

            return true;
        }
    }
}