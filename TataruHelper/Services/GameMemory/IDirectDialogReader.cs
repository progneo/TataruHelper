using Sharlayan.Core;
using Sharlayan.Models.ReadResults;

namespace FFXIVTataruHelper.Services.GameMemory
{
    public interface IDirectDialogReader
    {
        ChatLogResult ExtractDirectDialog(ChatLogResult chatLogResult);

        bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2);
    }
}
