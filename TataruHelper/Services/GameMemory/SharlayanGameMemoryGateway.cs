using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
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
        private const string TalkAddonName = "Talk";
        private const long Utf8StringPointerOffset = 0;
        private const long Utf8StringBufUsedOffset = 16;
        private const long Utf8StringLengthOffset = 24;
        private const long Utf8StringInlineFlagOffset = 33;
        private const long Utf8StringInlineBufferOffset = 34;
        private const int MaxUtf8StringByteLength = 4096;
        private const int MaxAtkUnitListEntries = 256;

        private static readonly Lazy<UiDirectDialogOffsets> _uiDirectDialogOffsets =
            new Lazy<UiDirectDialogOffsets>(ResolveUiDirectDialogOffsets);

        private readonly IDirectDialogReader _directDialogReader;
        private readonly IAppLogger _logger;
        private readonly Func<DateTime> _timestampProvider;
        private readonly Func<RealtimeDirectDialogReadResult> _realtimeDirectDialogReaderOverride;

        private MemoryHandler _memoryHandler;
        private Reader _reader;
        private ChatLogResult _lastChatLogResult = new ChatLogResult();
        private string _lastRealtimeDialogSignature = string.Empty;

        public SharlayanGameMemoryGateway(IDirectDialogReader directDialogReader, IAppLogger logger)
            : this(directDialogReader, logger, null, null)
        {
        }

        internal SharlayanGameMemoryGateway(
            IDirectDialogReader directDialogReader,
            IAppLogger logger,
            Func<RealtimeDirectDialogReadResult> realtimeDirectDialogReaderOverride,
            Func<DateTime> timestampProvider)
        {
            _directDialogReader = directDialogReader;
            _logger = logger;
            _realtimeDirectDialogReaderOverride = realtimeDirectDialogReaderOverride;
            _timestampProvider = timestampProvider ?? (() => DateTime.Now);
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
            _lastRealtimeDialogSignature = string.Empty;
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
                _lastRealtimeDialogSignature = string.Empty;
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
            var fallbackDirectDialog = _directDialogReader.ExtractDirectDialog(_lastChatLogResult) ?? new ChatLogResult();
            var realtimeResult = _realtimeDirectDialogReaderOverride != null
                ? _realtimeDirectDialogReaderOverride()
                : TryReadRealtimeDirectDialog();

            if (!realtimeResult.SourceAvailable)
            {
                return fallbackDirectDialog;
            }

            var result = new ChatLogResult();
            if (realtimeResult.DialogItem != null)
            {
                result.ChatLogItems.Enqueue(realtimeResult.DialogItem);
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

        internal static string BuildRealtimeSignature(string dialogLine)
        {
            return (dialogLine ?? string.Empty).Trim();
        }

        internal static string SelectBestTalkText(IEnumerable<string> candidates)
        {
            if (candidates == null)
            {
                return string.Empty;
            }

            return candidates
                .Select(candidate => (candidate ?? string.Empty).Trim())
                .Where(candidate => candidate.Length > 0)
                .OrderByDescending(candidate => candidate.Length)
                .FirstOrDefault() ?? string.Empty;
        }

        private static GameLanguage ParseGameLanguage(string gameLanguage)
        {
            if (Enum.TryParse(gameLanguage, true, out GameLanguage language))
            {
                return language;
            }

            return GameLanguage.English;
        }

        private RealtimeDirectDialogReadResult TryReadRealtimeDirectDialog()
        {
            if (_memoryHandler == null)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            var locations = _memoryHandler.Scanner?.Locations;
            if (locations == null)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            if (!_uiDirectDialogOffsets.Value.IsValid)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            if (!locations.TryGetValue(Signatures.CHATLOG_KEY, out var chatLogLocation) || chatLogLocation == null)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            var chatLogAddress = chatLogLocation.GetAddress();
            if (chatLogAddress == IntPtr.Zero)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            var uiModuleAddress = SubtractAddress(chatLogAddress, _uiDirectDialogOffsets.Value.RaptureLogModuleOffset);
            if (uiModuleAddress == IntPtr.Zero)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            var raptureAtkModuleAddress = AddAddress(uiModuleAddress, _uiDirectDialogOffsets.Value.RaptureAtkModuleOffset);
            if (raptureAtkModuleAddress == IntPtr.Zero)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            var atkUnitManagerAddress = _memoryHandler.ReadPointer(raptureAtkModuleAddress, _uiDirectDialogOffsets.Value.AtkUnitManagerOffset);
            if (atkUnitManagerAddress == IntPtr.Zero)
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            if (!TryGetTalkAddonAddress(atkUnitManagerAddress, out var talkAddonAddress))
            {
                return RealtimeDirectDialogReadResult.Available(null);
            }

            if (!TryReadAddonTalkText(talkAddonAddress, out var talkText))
            {
                return RealtimeDirectDialogReadResult.Unavailable();
            }

            if (talkText.Length == 0)
            {
                return RealtimeDirectDialogReadResult.Available(null);
            }

            var signature = BuildRealtimeSignature(talkText);
            if (string.Equals(_lastRealtimeDialogSignature, signature, StringComparison.Ordinal))
            {
                return RealtimeDirectDialogReadResult.Available(null);
            }

            _lastRealtimeDialogSignature = signature;

            return RealtimeDirectDialogReadResult.Available(new ChatLogItem
            {
                Code = DirectDialogCode,
                Line = talkText,
                TimeStamp = _timestampProvider()
            });
        }

        private bool TryGetTalkAddonAddress(IntPtr atkUnitManagerAddress, out IntPtr talkAddonAddress)
        {
            talkAddonAddress = IntPtr.Zero;
            var allLoadedUnitsListAddress = AddAddress(atkUnitManagerAddress, _uiDirectDialogOffsets.Value.AllLoadedUnitsListOffset);
            if (allLoadedUnitsListAddress == IntPtr.Zero)
            {
                return false;
            }

            var loadedUnitsCount = _memoryHandler.GetUInt16(allLoadedUnitsListAddress, _uiDirectDialogOffsets.Value.AtkUnitListCountOffset);
            if (loadedUnitsCount <= 0)
            {
                return true;
            }

            var entriesAddress = AddAddress(allLoadedUnitsListAddress, _uiDirectDialogOffsets.Value.AtkUnitListEntriesOffset);
            if (entriesAddress == IntPtr.Zero)
            {
                return false;
            }

            var safeCount = Math.Min((int)loadedUnitsCount, MaxAtkUnitListEntries);
            for (var i = 0; i < safeCount; i++)
            {
                var entryOffset = (long)i * IntPtr.Size;
                var addonAddress = _memoryHandler.ReadPointer(entriesAddress, entryOffset);
                if (addonAddress == IntPtr.Zero)
                {
                    continue;
                }

                if (!TryReadAddonName(addonAddress, out var addonName))
                {
                    continue;
                }

                if (string.Equals(addonName, TalkAddonName, StringComparison.OrdinalIgnoreCase))
                {
                    talkAddonAddress = addonAddress;
                    return true;
                }
            }

            return true;
        }

        private bool TryReadAddonName(IntPtr addonAddress, out string addonName)
        {
            addonName = string.Empty;
            var nameAddress = AddAddress(addonAddress, _uiDirectDialogOffsets.Value.AtkUnitBaseNameOffset);
            if (nameAddress == IntPtr.Zero || _uiDirectDialogOffsets.Value.AtkUnitBaseNameLength <= 0)
            {
                return false;
            }

            var buffer = _memoryHandler.GetByteArray(nameAddress, _uiDirectDialogOffsets.Value.AtkUnitBaseNameLength);
            if (buffer == null || buffer.Length == 0)
            {
                return false;
            }

            var terminatorIndex = Array.IndexOf(buffer, (byte)0);
            var length = terminatorIndex >= 0 ? terminatorIndex : buffer.Length;
            if (length <= 0)
            {
                return true;
            }

            addonName = Encoding.ASCII.GetString(buffer, 0, length).Trim();
            return true;
        }

        private bool TryReadAddonTalkText(IntPtr talkAddonAddress, out string talkText)
        {
            talkText = string.Empty;
            var textCandidates = new List<string>();

            foreach (var textNodeOffset in _uiDirectDialogOffsets.Value.AddonTalkTextNodeOffsets)
            {
                var textNodeAddress = _memoryHandler.ReadPointer(talkAddonAddress, textNodeOffset);
                if (textNodeAddress == IntPtr.Zero)
                {
                    continue;
                }

                if (!TryReadUtf8String(textNodeAddress, _uiDirectDialogOffsets.Value.AtkTextNodeNodeTextOffset, out var candidate))
                {
                    continue;
                }

                textCandidates.Add(candidate);
            }

            talkText = SelectBestTalkText(textCandidates);
            return true;
        }

        private bool TryReadUtf8String(IntPtr baseAddress, long structOffset, out string value)
        {
            value = string.Empty;
            var utf8StringAddress = AddAddress(baseAddress, structOffset);
            if (utf8StringAddress == IntPtr.Zero)
            {
                return false;
            }

            var stringLength = _memoryHandler.GetInt64(utf8StringAddress, Utf8StringLengthOffset);
            var bytesUsed = _memoryHandler.GetInt64(utf8StringAddress, Utf8StringBufUsedOffset);
            var byteCount = bytesUsed > 0 ? bytesUsed : stringLength;

            if (byteCount <= 0)
            {
                return true;
            }

            if (byteCount > MaxUtf8StringByteLength)
            {
                return false;
            }

            var isUsingInlineBuffer = _memoryHandler.GetByte(utf8StringAddress, Utf8StringInlineFlagOffset) != 0;
            var dataAddress = isUsingInlineBuffer
                ? AddAddress(utf8StringAddress, Utf8StringInlineBufferOffset)
                : _memoryHandler.ReadPointer(utf8StringAddress, Utf8StringPointerOffset);

            if (dataAddress == IntPtr.Zero)
            {
                return false;
            }

            var effectiveByteCount = (int)byteCount;

            var data = _memoryHandler.GetByteArray(dataAddress, effectiveByteCount);
            if (data == null || data.Length == 0)
            {
                return true;
            }

            var zeroTerminatorIndex = Array.IndexOf(data, (byte)0);
            if (zeroTerminatorIndex >= 0)
            {
                effectiveByteCount = zeroTerminatorIndex;
            }

            if (effectiveByteCount <= 0)
            {
                return true;
            }

            value = Encoding.UTF8.GetString(data, 0, effectiveByteCount);
            return true;
        }

        private static IntPtr AddAddress(IntPtr address, long offset)
        {
            var target = address.ToInt64() + offset;
            return target <= 0 ? IntPtr.Zero : new IntPtr(target);
        }

        private static IntPtr SubtractAddress(IntPtr address, long offset)
        {
            var target = address.ToInt64() - offset;
            return target <= 0 ? IntPtr.Zero : new IntPtr(target);
        }

        private static bool IsSpecificCode(ChatLogItem item, string code)
        {
            return item != null &&
                   !string.IsNullOrEmpty(item.Code) &&
                   string.Equals(item.Code, code, StringComparison.OrdinalIgnoreCase);
        }

        private static UiDirectDialogOffsets ResolveUiDirectDialogOffsets()
        {
            var uiModuleType = Type.GetType("FFXIVClientStructs.FFXIV.Client.UI.UIModule, Sharlayan");
            if (uiModuleType == null)
            {
                return UiDirectDialogOffsets.Empty;
            }

            var raptureLogModuleOffset = ResolveFieldOffset(uiModuleType, "RaptureLogModule");
            var raptureAtkModuleOffset = ResolveFieldOffset(uiModuleType, "RaptureAtkModule");
            var raptureAtkModuleType = Type.GetType("FFXIVClientStructs.FFXIV.Client.UI.RaptureAtkModule, Sharlayan");
            var atkUnitManagerType = Type.GetType("FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitManager, Sharlayan");
            var atkUnitListType = Type.GetType("FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitList, Sharlayan");
            var atkUnitBaseType = Type.GetType("FFXIVClientStructs.FFXIV.Component.GUI.AtkUnitBase, Sharlayan");
            var addonTalkType = Type.GetType("FFXIVClientStructs.FFXIV.Client.UI.AddonTalk, Sharlayan");
            var atkTextNodeType = Type.GetType("FFXIVClientStructs.FFXIV.Component.GUI.AtkTextNode, Sharlayan");

            if (raptureAtkModuleType == null ||
                atkUnitManagerType == null ||
                atkUnitListType == null ||
                atkUnitBaseType == null ||
                addonTalkType == null ||
                atkTextNodeType == null)
            {
                return UiDirectDialogOffsets.Empty;
            }

            var atkUnitManagerOffset = ResolveFieldOffset(raptureAtkModuleType, "AtkUnitManager");
            var allLoadedUnitsListOffset = ResolveFieldOffset(atkUnitManagerType, "AllLoadedUnitsList");
            var atkUnitListEntriesOffset = ResolveFieldOffset(atkUnitListType, "_entries");
            var atkUnitListCountOffset = ResolveFieldOffset(atkUnitListType, "Count");
            var atkUnitBaseNameOffset = ResolveFieldOffset(atkUnitBaseType, "_name");
            var atkUnitBaseNameLength = ResolveFixedBufferLength(atkUnitBaseType, "_name");
            var atkTextNodeNodeTextOffset = ResolveFieldOffset(atkTextNodeType, "NodeText");
            var addonTalkTextNodeOffsets = ResolveAddonTalkTextNodeOffsets(addonTalkType);

            if (raptureLogModuleOffset < 0 ||
                raptureAtkModuleOffset < 0 ||
                atkUnitManagerOffset < 0 ||
                allLoadedUnitsListOffset < 0 ||
                atkUnitListEntriesOffset < 0 ||
                atkUnitListCountOffset < 0 ||
                atkUnitBaseNameOffset < 0 ||
                atkUnitBaseNameLength <= 0 ||
                atkTextNodeNodeTextOffset < 0 ||
                addonTalkTextNodeOffsets.Length == 0)
            {
                return UiDirectDialogOffsets.Empty;
            }

            return new UiDirectDialogOffsets(
                raptureLogModuleOffset,
                raptureAtkModuleOffset,
                atkUnitManagerOffset,
                allLoadedUnitsListOffset,
                atkUnitListEntriesOffset,
                atkUnitListCountOffset,
                atkUnitBaseNameOffset,
                atkUnitBaseNameLength,
                atkTextNodeNodeTextOffset,
                addonTalkTextNodeOffsets);
        }

        private static long ResolveFieldOffset(Type type, string fieldName)
        {
            var field = type.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null)
            {
                return -1;
            }

            var fieldOffsetAttribute = field.GetCustomAttributes(typeof(FieldOffsetAttribute), false)
                .OfType<FieldOffsetAttribute>()
                .FirstOrDefault();
            if (fieldOffsetAttribute != null)
            {
                return fieldOffsetAttribute.Value;
            }

            return -1;
        }

        private static int ResolveFixedBufferLength(Type ownerType, string fieldName)
        {
            var field = ownerType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null || field.FieldType == null)
            {
                return 0;
            }

            var typeName = field.FieldType.Name ?? string.Empty;
            const string prefix = "FixedSizeArray";
            if (!typeName.StartsWith(prefix, StringComparison.Ordinal))
            {
                return 0;
            }

            var digits = new string(typeName.Skip(prefix.Length).TakeWhile(char.IsDigit).ToArray());
            return int.TryParse(digits, out var length) ? length : 0;
        }

        private static long[] ResolveAddonTalkTextNodeOffsets(Type addonTalkType)
        {
            return addonTalkType
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                .Where(field => field.Name.StartsWith("AtkTextNode", StringComparison.Ordinal))
                .Where(field => string.Equals(field.FieldType.Name, "AtkTextNode*", StringComparison.Ordinal))
                .Select(field =>
                {
                    var offsetAttribute = field.GetCustomAttributes(typeof(FieldOffsetAttribute), false)
                        .OfType<FieldOffsetAttribute>()
                        .FirstOrDefault();
                    return (long)(offsetAttribute?.Value ?? -1);
                })
                .Where(offset => offset >= 0)
                .OrderBy(offset => offset)
                .ToArray();
        }

        internal readonly struct RealtimeDirectDialogReadResult
        {
            public bool SourceAvailable { get; }
            public ChatLogItem DialogItem { get; }

            private RealtimeDirectDialogReadResult(bool sourceAvailable, ChatLogItem dialogItem)
            {
                SourceAvailable = sourceAvailable;
                DialogItem = dialogItem;
            }

            public static RealtimeDirectDialogReadResult Unavailable()
            {
                return new RealtimeDirectDialogReadResult(false, null);
            }

            public static RealtimeDirectDialogReadResult Available(ChatLogItem dialogItem)
            {
                return new RealtimeDirectDialogReadResult(true, dialogItem);
            }
        }

        private readonly struct UiDirectDialogOffsets
        {
            public static UiDirectDialogOffsets Empty =>
                new UiDirectDialogOffsets(-1, -1, -1, -1, -1, -1, -1, 0, -1, Array.Empty<long>());

            public long RaptureLogModuleOffset { get; }
            public long RaptureAtkModuleOffset { get; }
            public long AtkUnitManagerOffset { get; }
            public long AllLoadedUnitsListOffset { get; }
            public long AtkUnitListEntriesOffset { get; }
            public long AtkUnitListCountOffset { get; }
            public long AtkUnitBaseNameOffset { get; }
            public int AtkUnitBaseNameLength { get; }
            public long AtkTextNodeNodeTextOffset { get; }
            public long[] AddonTalkTextNodeOffsets { get; }

            public bool IsValid =>
                RaptureLogModuleOffset >= 0 &&
                RaptureAtkModuleOffset >= 0 &&
                AtkUnitManagerOffset >= 0 &&
                AllLoadedUnitsListOffset >= 0 &&
                AtkUnitListEntriesOffset >= 0 &&
                AtkUnitListCountOffset >= 0 &&
                AtkUnitBaseNameOffset >= 0 &&
                AtkUnitBaseNameLength > 0 &&
                AtkTextNodeNodeTextOffset >= 0 &&
                AddonTalkTextNodeOffsets != null &&
                AddonTalkTextNodeOffsets.Length > 0;

            public UiDirectDialogOffsets(
                long raptureLogModuleOffset,
                long raptureAtkModuleOffset,
                long atkUnitManagerOffset,
                long allLoadedUnitsListOffset,
                long atkUnitListEntriesOffset,
                long atkUnitListCountOffset,
                long atkUnitBaseNameOffset,
                int atkUnitBaseNameLength,
                long atkTextNodeNodeTextOffset,
                long[] addonTalkTextNodeOffsets)
            {
                RaptureLogModuleOffset = raptureLogModuleOffset;
                RaptureAtkModuleOffset = raptureAtkModuleOffset;
                AtkUnitManagerOffset = atkUnitManagerOffset;
                AllLoadedUnitsListOffset = allLoadedUnitsListOffset;
                AtkUnitListEntriesOffset = atkUnitListEntriesOffset;
                AtkUnitListCountOffset = atkUnitListCountOffset;
                AtkUnitBaseNameOffset = atkUnitBaseNameOffset;
                AtkUnitBaseNameLength = atkUnitBaseNameLength;
                AtkTextNodeNodeTextOffset = atkTextNodeNodeTextOffset;
                AddonTalkTextNodeOffsets = addonTalkTextNodeOffsets;
            }
        }
    }
}
