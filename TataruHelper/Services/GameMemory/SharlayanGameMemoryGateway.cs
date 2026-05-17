using System;
using System.Collections.Generic;
using System.Linq;

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
        private const string DirectDialogCode = "003D";
        private const string CutsceneDialogCode = "0044";

        private readonly IDirectDialogReader _directDialogReader;
        private readonly IAppLogger _logger;
        private readonly Func<DateTime> _timestampProvider;
        private readonly Func<TalkAddonRealtimeDialogSnapshot> _realtimeDialogSnapshotOverride;

        private MemoryHandler _memoryHandler;
        private Reader _reader;
        private TalkAddonRealtimeReader _talkAddonRealtimeReader;
        private ChatLogResult _lastChatLogResult = new ChatLogResult();
        private string _lastRealtimeDialogSignature = string.Empty;

        public SharlayanGameMemoryGateway(IDirectDialogReader directDialogReader, IAppLogger logger)
            : this(directDialogReader, logger, null, null)
        {
        }

        internal SharlayanGameMemoryGateway(
            IDirectDialogReader directDialogReader,
            IAppLogger logger,
            Func<TalkAddonRealtimeDialogSnapshot> realtimeDialogSnapshotOverride,
            Func<DateTime> timestampProvider)
        {
            _directDialogReader = directDialogReader;
            _logger = logger;
            _realtimeDialogSnapshotOverride = realtimeDialogSnapshotOverride;
            _timestampProvider = timestampProvider ?? (() => DateTime.Now);
        }

        public void SetProcess(ProcessModel processModel, string gameLanguage, string patchVersion, bool useLocalCache,
            bool scanAllMemoryRegions)
        {
            var configuration = new SharlayanConfiguration
            {
                ProcessModel = processModel,
                GameLanguage = ParseGameLanguage(gameLanguage),
                ScanAllRegions = scanAllMemoryRegions,
                IgnoreGameVersionMismatch = true,
                ResourceProvider = ResourceProviderKind.FFXIVClientStructsDirect
            };

            UnsetProcessCore();

            _memoryHandler = new MemoryHandler(configuration);
            _reader = _memoryHandler.Reader;
            _talkAddonRealtimeReader = new TalkAddonRealtimeReader(_memoryHandler);
            _lastChatLogResult = new ChatLogResult();
            _lastRealtimeDialogSignature = string.Empty;
        }

        public void UnsetProcess()
        {
            UnsetProcessCore();
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
            var fallbackDirectDialog =
                _directDialogReader.ExtractDirectDialog(_lastChatLogResult) ?? new ChatLogResult();
            var realtimeSnapshot = _realtimeDialogSnapshotOverride != null
                ? _realtimeDialogSnapshotOverride()
                : (_talkAddonRealtimeReader?.TryReadSnapshot() ?? TalkAddonRealtimeDialogSnapshot.Unavailable());

            if (!realtimeSnapshot.SourceAvailable)
            {
                return fallbackDirectDialog;
            }

            var result = new ChatLogResult();
            var talkText = NormalizeDialogToken(realtimeSnapshot.TalkText);
            if (talkText.Length > 0)
            {
                var signature = BuildRealtimeSignature(talkText);
                if (!string.Equals(_lastRealtimeDialogSignature, signature, StringComparison.Ordinal))
                {
                    _lastRealtimeDialogSignature = signature;
                    var line = BuildRealtimeDialogLine(talkText);
                    if (line.Length > 0)
                    {
                        result.ChatLogItems.Enqueue(new ChatLogItem
                        {
                            Code = DirectDialogCode, Line = line, TimeStamp = _timestampProvider()
                        });
                    }
                }
            }

            if (fallbackDirectDialog.ChatLogItems == null || fallbackDirectDialog.ChatLogItems.Count == 0)
            {
                return result;
            }

            foreach (var chatLogItem in fallbackDirectDialog.ChatLogItems.ToArray())
            {
                if (IsSpecificCode(chatLogItem, CutsceneDialogCode))
                {
                    result.ChatLogItems.Enqueue(chatLogItem);
                }
            }

            return result;
        }

        public bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2)
        {
            return _directDialogReader.CheckChatEquality(item1, item2);
        }

        public void Dispose()
        {
            UnsetProcess();
        }

        private void UnsetProcessCore()
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
                _talkAddonRealtimeReader = null;
                _lastChatLogResult = new ChatLogResult();
                _lastRealtimeDialogSignature = string.Empty;
            }
        }

        internal static string BuildRealtimeSignature(string dialogLine)
        {
            return NormalizeDialogToken(dialogLine);
        }

        internal static string SelectBestTalkText(IEnumerable<string> candidates)
        {
            if (candidates == null)
            {
                return string.Empty;
            }

            return candidates
                .Select(NormalizeDialogToken)
                .Where(candidate => candidate.Length > 0)
                .OrderByDescending(candidate => candidate.Length)
                .FirstOrDefault() ?? string.Empty;
        }

        internal static string NormalizeDialogToken(string value)
        {
            return (value ?? string.Empty).Trim();
        }

        internal static string BuildRealtimeDialogLine(string talkText)
        {
            var normalizedTalkText = NormalizeDialogToken(talkText);
            if (normalizedTalkText.Length == 0)
            {
                return string.Empty;
            }

            return normalizedTalkText;
        }

        private static GameLanguage ParseGameLanguage(string gameLanguage)
        {
            if (Enum.TryParse(gameLanguage, true, out GameLanguage language))
            {
                return language;
            }

            return GameLanguage.English;
        }

        private static bool IsSpecificCode(ChatLogItem item, string code)
        {
            return item != null &&
                   !string.IsNullOrEmpty(item.Code) &&
                   string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase);
        }
    }
}