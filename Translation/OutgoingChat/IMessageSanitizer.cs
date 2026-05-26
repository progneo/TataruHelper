namespace Translation.OutgoingChat
{
    public interface IMessageSanitizer
    {
        string Sanitize(string text);

        int Utf8ByteLength(string text);
    }
}