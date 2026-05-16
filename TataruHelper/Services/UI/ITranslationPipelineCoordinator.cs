using FFXIVTataruHelper.FFHandlers;

namespace FFXIVTataruHelper.Services.UI
{
    public interface ITranslationPipelineCoordinator
    {
        void Start(IFFMemoryReaderService memoryReader, ChatProcessor chatProcessor);

        void Stop();
    }
}
