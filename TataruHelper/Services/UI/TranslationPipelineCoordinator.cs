using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.Logging;
using System;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class TranslationPipelineCoordinator : ITranslationPipelineCoordinator
    {
        private readonly IAppLogger _logger;
        private IFFMemoryReaderService _memoryReader;
        private ChatProcessor _chatProcessor;
        private bool _isStarted;

        public TranslationPipelineCoordinator(IAppLogger logger)
        {
            _logger = logger;
        }

        public void Start(IFFMemoryReaderService memoryReader, ChatProcessor chatProcessor)
        {
            if (memoryReader == null)
            {
                throw new ArgumentNullException(nameof(memoryReader));
            }

            if (chatProcessor == null)
            {
                throw new ArgumentNullException(nameof(chatProcessor));
            }

            if (_isStarted)
            {
                return;
            }

            _memoryReader = memoryReader;
            _chatProcessor = chatProcessor;
            _memoryReader.FFChatMessageArrived += _chatProcessor.OnFFChatMessageArrived;
            _isStarted = true;
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            try
            {
                _memoryReader.FFChatMessageArrived -= _chatProcessor.OnFFChatMessageArrived;
            }
            catch (Exception ex)
            {
                _logger.WriteLog("Failed to detach translation pipeline.");
                _logger.WriteLog(ex);
            }
            finally
            {
                _memoryReader = null;
                _chatProcessor = null;
                _isStarted = false;
            }
        }
    }
}
