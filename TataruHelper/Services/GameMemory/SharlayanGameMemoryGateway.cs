using System;
using FFXIVTataruHelper.Services.Logging;
using Sharlayan;
using Sharlayan.Core;
using Sharlayan.Enums;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;
using Sharlayan.Resources;

namespace FFXIVTataruHelper.Services.GameMemory
{
    public sealed class SharlayanGameMemoryGateway : IGameMemoryGateway, IDisposable
    {
        private readonly IDirectDialogReader _directDialogReader;
        private readonly IAppLogger _logger;

        private MemoryHandler _memoryHandler;
        private Reader _reader;
        private ChatLogResult _lastChatLogResult = new ChatLogResult();

        public SharlayanGameMemoryGateway(IDirectDialogReader directDialogReader, IAppLogger logger)
        {
            _directDialogReader = directDialogReader;
            _logger = logger;
        }

        public void SetProcess(ProcessModel processModel, string gameLanguage, string patchVersion, bool useLocalCache, bool scanAllMemoryRegions)
        {
            UnsetProcess();

            var configuration = new SharlayanConfiguration
            {
                ProcessModel = processModel,
                GameLanguage = ParseGameLanguage(gameLanguage),
                ScanAllRegions = scanAllMemoryRegions,
                IgnoreGameVersionMismatch = true,
                ResourceProvider = ResourceProviderKind.FFXIVClientStructsDirect
            };

            _memoryHandler = new MemoryHandler(configuration);
            _reader = _memoryHandler.Reader;
            _lastChatLogResult = new ChatLogResult();
        }

        public void UnsetProcess()
        {
            try
            {
                _memoryHandler?.Dispose();
            }
            catch (Exception ex)
            {
                _logger.WriteLog(ex);
            }
            finally
            {
                _memoryHandler = null;
                _reader = null;
                _lastChatLogResult = new ChatLogResult();
            }
        }

        public ChatLogResult GetChatLog(int previousArrayIndex, int previousOffset)
        {
            if (_reader == null)
            {
                return new ChatLogResult();
            }

            _lastChatLogResult = _reader.GetChatLog(previousArrayIndex, previousOffset) ?? new ChatLogResult();
            return _lastChatLogResult;
        }

        public ChatLogResult GetDirectDialog()
        {
            return _directDialogReader.ExtractDirectDialog(_lastChatLogResult);
        }

        public bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2)
        {
            return _directDialogReader.CheckChatEquality(item1, item2);
        }

        public void Dispose()
        {
            UnsetProcess();
        }

        private static GameLanguage ParseGameLanguage(string gameLanguage)
        {
            if (Enum.TryParse(gameLanguage, true, out GameLanguage language))
            {
                return language;
            }

            return GameLanguage.English;
        }
    }
}
