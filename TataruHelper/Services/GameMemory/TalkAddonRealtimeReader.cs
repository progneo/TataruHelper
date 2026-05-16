using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;

using Sharlayan;

namespace FFXIVTataruHelper.Services.GameMemory
{
    internal sealed class TalkAddonRealtimeReader
    {
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

        private readonly MemoryHandler _memoryHandler;

        public TalkAddonRealtimeReader(MemoryHandler memoryHandler)
        {
            _memoryHandler = memoryHandler;
        }

        public TalkAddonRealtimeDialogSnapshot TryReadSnapshot()
        {
            if (_memoryHandler == null)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            var locations = _memoryHandler.Scanner?.Locations;
            if (locations == null || !_uiDirectDialogOffsets.Value.IsValid)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            if (!locations.TryGetValue(Signatures.CHATLOG_KEY, out var chatLogLocation) || chatLogLocation == null)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            var chatLogAddress = chatLogLocation.GetAddress();
            if (chatLogAddress == IntPtr.Zero)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            var uiModuleAddress = SubtractAddress(chatLogAddress, _uiDirectDialogOffsets.Value.RaptureLogModuleOffset);
            if (uiModuleAddress == IntPtr.Zero)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            var speakerCandidate = string.Empty;
            var speakerOffset = _uiDirectDialogOffsets.Value.LastTalkNameOffset;
            if (speakerOffset >= 0)
            {
                if (TryReadUtf8String(uiModuleAddress, speakerOffset, out var speaker))
                {
                    speakerCandidate = SharlayanGameMemoryGateway.NormalizeDialogToken(speaker);
                }
            }

            var raptureAtkModuleAddress =
                AddAddress(uiModuleAddress, _uiDirectDialogOffsets.Value.RaptureAtkModuleOffset);
            if (raptureAtkModuleAddress == IntPtr.Zero)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            var atkUnitManagerAddress = _memoryHandler.ReadPointer(raptureAtkModuleAddress,
                _uiDirectDialogOffsets.Value.AtkUnitManagerOffset);
            if (atkUnitManagerAddress == IntPtr.Zero)
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            if (!TryGetTalkAddonAddress(atkUnitManagerAddress, out var talkAddonAddress))
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            if (talkAddonAddress == IntPtr.Zero)
            {
                return TalkAddonRealtimeDialogSnapshot.Available(string.Empty, speakerCandidate, Array.Empty<string>());
            }

            if (!TryReadAddonTalkNodeTexts(talkAddonAddress, out var nodeTexts))
            {
                return TalkAddonRealtimeDialogSnapshot.Unavailable();
            }

            var talkText = SharlayanGameMemoryGateway.SelectBestTalkText(nodeTexts);
            return TalkAddonRealtimeDialogSnapshot.Available(talkText, speakerCandidate, nodeTexts);
        }

        private bool TryGetTalkAddonAddress(IntPtr atkUnitManagerAddress, out IntPtr talkAddonAddress)
        {
            talkAddonAddress = IntPtr.Zero;

            var allLoadedUnitsListAddress =
                AddAddress(atkUnitManagerAddress, _uiDirectDialogOffsets.Value.AllLoadedUnitsListOffset);
            if (allLoadedUnitsListAddress == IntPtr.Zero)
            {
                return false;
            }

            var loadedUnitsCount = _memoryHandler.GetUInt16(allLoadedUnitsListAddress,
                _uiDirectDialogOffsets.Value.AtkUnitListCountOffset);
            if (loadedUnitsCount <= 0)
            {
                return true;
            }

            var entriesAddress = AddAddress(allLoadedUnitsListAddress,
                _uiDirectDialogOffsets.Value.AtkUnitListEntriesOffset);
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

        private bool TryReadAddonTalkNodeTexts(IntPtr talkAddonAddress, out string[] nodeTexts)
        {
            var textCandidates = new List<string>();

            foreach (var textNodeOffset in _uiDirectDialogOffsets.Value.AddonTalkTextNodeOffsets)
            {
                var textNodeAddress = _memoryHandler.ReadPointer(talkAddonAddress, textNodeOffset);
                if (textNodeAddress == IntPtr.Zero)
                {
                    continue;
                }

                if (!TryReadUtf8String(textNodeAddress, _uiDirectDialogOffsets.Value.AtkTextNodeNodeTextOffset,
                        out var candidate))
                {
                    continue;
                }

                var normalized = SharlayanGameMemoryGateway.NormalizeDialogToken(candidate);
                if (normalized.Length > 0)
                {
                    textCandidates.Add(normalized);
                }
            }

            nodeTexts = textCandidates.ToArray();
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

        private static UiDirectDialogOffsets ResolveUiDirectDialogOffsets()
        {
            var uiModuleType = Type.GetType("FFXIVClientStructs.FFXIV.Client.UI.UIModule, Sharlayan");
            if (uiModuleType == null)
            {
                return UiDirectDialogOffsets.Empty;
            }

            var raptureLogModuleOffset = ResolveFieldOffset(uiModuleType, "RaptureLogModule");
            var lastTalkNameOffset = ResolveFieldOffset(uiModuleType, "LastTalkName");
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
                addonTalkTextNodeOffsets,
                lastTalkNameOffset);
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
            var field = ownerType.GetField(fieldName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
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

        private readonly struct UiDirectDialogOffsets
        {
            public static UiDirectDialogOffsets Empty =>
                new UiDirectDialogOffsets(-1, -1, -1, -1, -1, -1, -1, 0, -1, Array.Empty<long>(), -1);

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
            public long LastTalkNameOffset { get; }

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
                long[] addonTalkTextNodeOffsets,
                long lastTalkNameOffset)
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
                LastTalkNameOffset = lastTalkNameOffset;
            }
        }
    }

    internal readonly struct TalkAddonRealtimeDialogSnapshot
    {
        public bool SourceAvailable { get; }
        public string TalkText { get; }
        public string SpeakerCandidate { get; }
        public string[] NodeTexts { get; }

        private TalkAddonRealtimeDialogSnapshot(bool sourceAvailable, string talkText, string speakerCandidate,
            string[] nodeTexts)
        {
            SourceAvailable = sourceAvailable;
            TalkText = talkText ?? string.Empty;
            SpeakerCandidate = speakerCandidate ?? string.Empty;
            NodeTexts = nodeTexts ?? Array.Empty<string>();
        }

        public static TalkAddonRealtimeDialogSnapshot Unavailable()
        {
            return new TalkAddonRealtimeDialogSnapshot(false, string.Empty, string.Empty, Array.Empty<string>());
        }

        public static TalkAddonRealtimeDialogSnapshot Available(string talkText, string speakerCandidate,
            string[] nodeTexts)
        {
            return new TalkAddonRealtimeDialogSnapshot(true, talkText, speakerCandidate, nodeTexts);
        }
    }
}