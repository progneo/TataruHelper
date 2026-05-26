using System.Threading;
using System.Threading.Tasks;

namespace FFXIVTataruHelper.Services.OutgoingChat
{
    public interface IOutgoingChatService
    {
        Task<OutgoingChatResult>
            TranslateAndCopyAsync(OutgoingChatRequest request, CancellationToken cancellationToken);
    }
}