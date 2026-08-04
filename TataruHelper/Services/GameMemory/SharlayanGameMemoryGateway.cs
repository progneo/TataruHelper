using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        private const string RealtimeDirectDialogCode = "F03D";
        private const string RealtimeCutsceneDialogCode = "F044";

        private readonly IDirectDialogReader _directDialogReader;
        private readonly IAppLogger _logger;
        private readonly Func<DateTime> _timestampProvider;
        private readonly Func<TalkAddonRealtimeDialogSnapshot> _realtimeDialogSnapshotOverride;

        private MemoryHandler _memoryHandler;
        private Reader _reader;
        private TalkAddonRealtimeReader _talkAddonRealtimeReader;
        private ChatLogResult _lastChatLogResult = new ChatLogResult();
        private string _lastRealtimeDialogSignature = string.Empty;

        /// <summary>
        /// The Talk addon keeps its last line long after the dialogue box is
        /// dismissed. Without this, attaching to an already-running game reported
        /// whatever conversation the player had before as if it had just happened.
        /// The first snapshot after attaching is therefore recorded, not emitted.
        ///
        /// Only attaching arms it, so a caller feeding snapshots directly still
        /// gets every one of them.
        /// </summary>
        private bool _realtimeDialogPrimed = true;

        private const int MaxRememberedRealtimeLines = 64;

        private readonly HashSet<string> _recentRealtimeLines = new HashSet<string>(StringComparer.Ordinal);

        private readonly Queue<string> _recentRealtimeLineOrder = new Queue<string>();

        /// <summary>Bare text of the last realtime line, without the speaker prefix.</summary>
        private string _lastEmittedRealtimeText = string.Empty;

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
            ResetRealtimeDialogState();
        }

        /// <summary>
        /// Arms the priming that swallows the line the Talk addon was already
        /// holding when we attached. Separate from <see cref="SetProcess"/> so it
        /// can be exercised without a live game process.
        /// </summary>
        internal void ResetRealtimeDialogState()
        {
            _lastChatLogResult = new ChatLogResult();
            _lastRealtimeDialogSignature = string.Empty;
            _realtimeDialogPrimed = false;
            _lastEmittedRealtimeText = string.Empty;
            _recentRealtimeLines.Clear();
            _recentRealtimeLineOrder.Clear();
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
            DropLinesAlreadySeenLive(_lastChatLogResult);
            return _lastChatLogResult;
        }

        /// <summary>
        /// Removes dialogue the realtime reader already reported.
        ///
        /// An NPC line reaches us twice: once from the Talk addon while the bubble
        /// is on screen, and again from the chat log once the player clicks through.
        /// Both codes are enabled by default, so every line was translated and shown
        /// twice.
        /// </summary>
        private void DropLinesAlreadySeenLive(ChatLogResult chatLogResult)
        {
            if (_recentRealtimeLines.Count == 0 || chatLogResult?.ChatLogItems == null)
            {
                return;
            }

            var kept = chatLogResult.ChatLogItems
                .Where(item => !IsDuplicateOfRealtimeLine(item))
                .ToArray();

            if (kept.Length == chatLogResult.ChatLogItems.Count)
            {
                return;
            }

            chatLogResult.ChatLogItems.Clear();
            foreach (var item in kept)
            {
                chatLogResult.ChatLogItems.Enqueue(item);
            }
        }

        private bool IsDuplicateOfRealtimeLine(ChatLogItem item)
        {
            if (!IsSpecificCode(item, DirectDialogCode) && !IsSpecificCode(item, CutsceneDialogCode))
            {
                return false;
            }

            return _recentRealtimeLines.Contains(BuildDuplicateKey(item?.Line));
        }

        /// <summary>
        /// Reduces a dialogue line to just its spoken text so the live copy and the
        /// chat-log copy compare equal. The two render the speaker differently, so
        /// matching whole lines let every NPC line through twice.
        /// </summary>
        internal static string BuildDuplicateKey(string line)
        {
            var normalized = NormalizeDialogToken(line);
            if (normalized.Length == 0)
            {
                return string.Empty;
            }

            var separatorIndex = normalized.IndexOf(':');
            if (separatorIndex > 0 && separatorIndex < normalized.Length - 1)
            {
                normalized = normalized.Substring(separatorIndex + 1);
            }

            var builder = new StringBuilder(normalized.Length);
            var lastWasSpace = false;
            foreach (var c in normalized)
            {
                if (char.IsWhiteSpace(c))
                {
                    lastWasSpace = true;
                    continue;
                }

                if (lastWasSpace && builder.Length > 0)
                {
                    builder.Append(' ');
                }

                lastWasSpace = false;
                builder.Append(char.ToLowerInvariant(c));
            }

            return builder.ToString();
        }

        private void RememberRealtimeLine(string line)
        {
            var normalized = BuildDuplicateKey(line);
            if (normalized.Length == 0)
            {
                return;
            }

            if (_recentRealtimeLines.Add(normalized))
            {
                _recentRealtimeLineOrder.Enqueue(normalized);
            }

            while (_recentRealtimeLineOrder.Count > MaxRememberedRealtimeLines)
            {
                _recentRealtimeLines.Remove(_recentRealtimeLineOrder.Dequeue());
            }
        }

        public ChatLogResult GetDirectDialog()
        {
            var fallbackDirectDialog =
                _directDialogReader.ExtractDirectDialog(_lastChatLogResult) ?? new ChatLogResult();
            var realtimeSnapshot = _realtimeDialogSnapshotOverride != null
                ? _realtimeDialogSnapshotOverride()
                : (_talkAddonRealtimeReader?.TryReadSnapshot(_lastEmittedRealtimeText)
                   ?? TalkAddonRealtimeDialogSnapshot.Unavailable());

            if (!realtimeSnapshot.SourceAvailable)
            {
                return fallbackDirectDialog;
            }

            var result = new ChatLogResult();
            var talkText = NormalizeDialogToken(realtimeSnapshot.TalkText);
            if (talkText.Length == 0)
            {
                return fallbackDirectDialog;
            }

            var chatCode = NormalizeDialogToken(realtimeSnapshot.ChatCode);
            if (chatCode.Length == 0)
            {
                chatCode = DirectDialogCode;
            }

            chatCode = MapRealtimeChatCode(chatCode);

            var speakerName = NormalizeDialogToken(realtimeSnapshot.SpeakerName);
            var signature = BuildRealtimeSignature(chatCode, speakerName, talkText);
            if (!string.Equals(_lastRealtimeDialogSignature, signature, StringComparison.Ordinal))
            {
                var wasPrimed = _realtimeDialogPrimed;
                _lastRealtimeDialogSignature = signature;
                _lastEmittedRealtimeText = talkText;
                _realtimeDialogPrimed = true;

                var line = wasPrimed ? BuildRealtimeDialogLine(speakerName, talkText) : string.Empty;

                if (Logger.RawDialogLogEnabled)
                {
                    Logger.WriteRawDialogLog(
                        $"Emit code=[{chatCode}] speaker=[{speakerName}] text=[{talkText}] line=[{line}]");
                }

                if (line.Length > 0)
                {
                    result.ChatLogItems.Enqueue(new ChatLogItem
                    {
                        Code = chatCode, Line = line, TimeStamp = _timestampProvider()
                    });
                }

                // Remembered even when priming swallowed the line, so the chat-log
                // copy of an already-seen conversation is dropped too.
                RememberRealtimeLine(line.Length > 0 ? line : BuildRealtimeDialogLine(speakerName, talkText));
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
                _realtimeDialogPrimed = true;
            }
        }

        internal static string BuildRealtimeSignature(string dialogLine)
        {
            return NormalizeDialogToken(dialogLine);
        }

        internal static string BuildRealtimeSignature(string chatCode, string speakerName, string talkText)
        {
            return string.Concat(
                NormalizeDialogToken(chatCode),
                "|",
                NormalizeDialogToken(speakerName),
                "|",
                NormalizeDialogToken(talkText));
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
            return BuildRealtimeDialogLine(string.Empty, talkText);
        }

        internal static string BuildRealtimeDialogLine(string speakerName, string talkText)
        {
            var normalizedTalkText = NormalizeDialogToken(talkText);
            if (normalizedTalkText.Length == 0)
            {
                return string.Empty;
            }

            var normalizedSpeakerName = NormalizeDialogToken(speakerName);
            if (normalizedSpeakerName.Length == 0)
            {
                return normalizedTalkText;
            }

            return string.Concat(normalizedSpeakerName, ":", normalizedTalkText);
        }

        internal static string MapRealtimeChatCode(string chatCode)
        {
            var normalizedChatCode = NormalizeDialogToken(chatCode);
            if (string.Equals(normalizedChatCode, DirectDialogCode, StringComparison.OrdinalIgnoreCase))
            {
                return RealtimeDirectDialogCode;
            }

            if (string.Equals(normalizedChatCode, CutsceneDialogCode, StringComparison.OrdinalIgnoreCase))
            {
                return RealtimeCutsceneDialogCode;
            }

            return normalizedChatCode;
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