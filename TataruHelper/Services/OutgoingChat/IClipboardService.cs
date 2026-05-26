namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public interface IClipboardService
    {
        bool TryGetText(out string text);

        bool TrySetText(string text);
    }
}