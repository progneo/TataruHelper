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
        private const string MiniTalkAddonName = "MiniTalk";
        private const string AlternateMiniTalkAddonName = "_MiniTalk";
        private const string DirectDialogCode = "003D";
        private const string CutsceneDialogCode = "0044";
        private const string UiNamespace = "FFXIVClientStructs.FFXIV.Client.UI.";
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

        private string _lastLoggedLastTalk = string.Empty;
        private string _lastLoggedAddonNodes = string.Empty;

        public TalkAddonRealtimeReader(MemoryHandler memoryHandler)
        {
            _memoryHandler = memoryHandler;
        }

        private static void WriteDistinctRawDialogLog(ref string lastPayload, string payload)
        {
            if (!Logger.RawDialogLogEnabled || string.IsNullOrEmpty(payload))
            {
                return;
            }

            if (string.Equals(lastPayload, payload, StringComparison.Ordinal))
            {
                return;
            }

            lastPayload = payload;
            Logger.WriteRawDialogLog(payload);
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

            TryReadLastTalk(uiModuleAddress, out var lastTalkName, out var lastTalkText);

            var raptureAtkModuleAddress =
                AddAddress(uiModuleAddress, _uiDirectDialogOffsets.Value.RaptureAtkModuleOffset);
            if (raptureAtkModuleAddress == IntPtr.Zero)
            {
                return SelectRealtimeSnapshot(lastTalkName, lastTalkText,
                    Array.Empty<TalkAddonRealtimeDialogSnapshot>());
            }

            var atkUnitManagerAddress = _memoryHandler.ReadPointer(raptureAtkModuleAddress,
                _uiDirectDialogOffsets.Value.AtkUnitManagerOffset);
            if (atkUnitManagerAddress == IntPtr.Zero)
            {
                return SelectRealtimeSnapshot(lastTalkName, lastTalkText,
                    Array.Empty<TalkAddonRealtimeDialogSnapshot>());
            }

            if (!TryReadLoadedAddonSnapshot(atkUnitManagerAddress, lastTalkName, lastTalkText, out var snapshot))
            {
                return SelectRealtimeSnapshot(lastTalkName, lastTalkText,
                    Array.Empty<TalkAddonRealtimeDialogSnapshot>());
            }

            return SelectRealtimeSnapshot(lastTalkName, lastTalkText, new[] { snapshot });
        }

        private bool TryReadLastTalk(IntPtr uiModuleAddress, out string speakerName, out string talkText)
        {
            speakerName = string.Empty;
            talkText = string.Empty;

            var readName = TryReadUtf8String(uiModuleAddress, _uiDirectDialogOffsets.Value.LastTalkNameOffset,
                out speakerName);
            var readText = TryReadUtf8String(uiModuleAddress, _uiDirectDialogOffsets.Value.LastTalkTextOffset,
                out talkText);

            if (Logger.RawDialogLogEnabled)
            {
                WriteDistinctRawDialogLog(ref _lastLoggedLastTalk,
                    $"LastTalk name=[{speakerName}] text=[{talkText}]");
            }

            speakerName = SharlayanGameMemoryGateway.NormalizeDialogToken(speakerName);
            talkText = SharlayanGameMemoryGateway.NormalizeDialogToken(talkText);
            return readName || readText;
        }

        private bool TryReadLoadedAddonSnapshot(
            IntPtr atkUnitManagerAddress,
            string speakerName,
            string lastTalkText,
            out TalkAddonRealtimeDialogSnapshot snapshot)
        {
            snapshot = TalkAddonRealtimeDialogSnapshot.Unavailable();

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
                return false;
            }

            var entriesAddress = AddAddress(allLoadedUnitsListAddress,
                _uiDirectDialogOffsets.Value.AtkUnitListEntriesOffset);
            if (entriesAddress == IntPtr.Zero)
            {
                return false;
            }

            var loadedAddons = new List<LoadedAddon>();
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

                loadedAddons.Add(new LoadedAddon(addonAddress, addonName));
            }

            var matchedEmptySource = false;
            foreach (var addonSpec in _uiDirectDialogOffsets.Value.AddonSpecs)
            {
                var loadedAddon = loadedAddons
                    .FirstOrDefault(addon =>
                        string.Equals(addonSpec.AddonName, addon.AddonName, StringComparison.OrdinalIgnoreCase));
                if (loadedAddon.AddonAddress == IntPtr.Zero)
                {
                    continue;
                }

                if (!TryReadAddonNodeTexts(loadedAddon.AddonAddress, addonSpec, out var nodeTexts))
                {
                    continue;
                }

                if (Logger.RawDialogLogEnabled)
                {
                    var joinedNodes = string.Join(" | ",
                        (nodeTexts ?? Array.Empty<string>()).Select(text => $"[{text}]"));
                    WriteDistinctRawDialogLog(ref _lastLoggedAddonNodes,
                        $"Addon=[{addonSpec.AddonName}] code=[{addonSpec.ChatCode}] nodes={{ {joinedNodes} }}");
                }

                var addonSnapshot = BuildAddonSnapshot(addonSpec, nodeTexts, speakerName, lastTalkText);
                if (SharlayanGameMemoryGateway.NormalizeDialogToken(addonSnapshot.TalkText).Length == 0)
                {
                    if (!matchedEmptySource)
                    {
                        snapshot = addonSnapshot;
                        matchedEmptySource = true;
                    }

                    continue;
                }

                snapshot = addonSnapshot;
                return true;
            }

            return matchedEmptySource;
        }

        internal static TalkAddonRealtimeDialogSnapshot BuildAddonSnapshot(
            string chatCode,
            string[] nodeTexts,
            string lastTalkName,
            string lastTalkText,
            bool allowNodeSpeaker)
        {
            var normalizedNodeTexts = (nodeTexts ?? Array.Empty<string>())
                .Select(SharlayanGameMemoryGateway.NormalizeDialogToken)
                .Where(text => text.Length > 0)
                .ToArray();
            var talkText = SharlayanGameMemoryGateway.SelectBestTalkText(normalizedNodeTexts);
            var speakerName = string.Empty;

            if (allowNodeSpeaker)
            {
                speakerName = normalizedNodeTexts
                    .FirstOrDefault(text =>
                        !string.Equals(text, talkText, StringComparison.Ordinal)
                    ) ?? string.Empty;
            }

            if (speakerName.Length == 0 && DialogTextMatches(lastTalkText, talkText))
            {
                speakerName = SharlayanGameMemoryGateway.NormalizeDialogToken(lastTalkName);
            }

            return TalkAddonRealtimeDialogSnapshot.Available(chatCode, speakerName, talkText);
        }

        private static TalkAddonRealtimeDialogSnapshot BuildAddonSnapshot(
            AddonRealtimeTextSpec addonSpec,
            string[] nodeTexts,
            string lastTalkName,
            string lastTalkText)
        {
            return BuildAddonSnapshot(
                addonSpec.ChatCode,
                nodeTexts,
                lastTalkName,
                lastTalkText,
                addonSpec.AllowNodeSpeaker);
        }

        internal static TalkAddonRealtimeDialogSnapshot SelectRealtimeSnapshot(
            string speakerName,
            string lastTalkText,
            IEnumerable<TalkAddonRealtimeDialogSnapshot> addonSnapshots)
        {
            var normalizedSpeakerName = SharlayanGameMemoryGateway.NormalizeDialogToken(speakerName);
            TalkAddonRealtimeDialogSnapshot firstEmptyAddonSnapshot = default;
            var hasEmptyAddonSnapshot = false;

            foreach (var addonSnapshot in addonSnapshots ?? Enumerable.Empty<TalkAddonRealtimeDialogSnapshot>())
            {
                if (!addonSnapshot.SourceAvailable)
                {
                    continue;
                }

                var addonText = SharlayanGameMemoryGateway.NormalizeDialogToken(addonSnapshot.TalkText);
                var addonSpeakerName = SharlayanGameMemoryGateway.NormalizeDialogToken(addonSnapshot.SpeakerName);
                if (addonText.Length > 0)
                {
                    if (addonSpeakerName.Length == 0 && DialogTextMatches(lastTalkText, addonText))
                    {
                        addonSpeakerName = normalizedSpeakerName;
                    }

                    return TalkAddonRealtimeDialogSnapshot.Available(
                        addonSnapshot.ChatCode,
                        addonSpeakerName,
                        addonText);
                }

                if (!hasEmptyAddonSnapshot)
                {
                    firstEmptyAddonSnapshot = addonSnapshot;
                    hasEmptyAddonSnapshot = true;
                }
            }

            var fallbackText = SharlayanGameMemoryGateway.NormalizeDialogToken(lastTalkText);
            if (fallbackText.Length > 0)
            {
                return TalkAddonRealtimeDialogSnapshot.Available(DirectDialogCode, normalizedSpeakerName, fallbackText);
            }

            return hasEmptyAddonSnapshot ? firstEmptyAddonSnapshot : TalkAddonRealtimeDialogSnapshot.Unavailable();
        }

        private static bool DialogTextMatches(string left, string right)
        {
            var normalizedLeft = SharlayanGameMemoryGateway.NormalizeDialogToken(left);
            var normalizedRight = SharlayanGameMemoryGateway.NormalizeDialogToken(right);
            return normalizedLeft.Length > 0 &&
                   normalizedRight.Length > 0 &&
                   string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
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

        private bool TryReadAddonNodeTexts(
            IntPtr addonAddress,
            AddonRealtimeTextSpec addonSpec,
            out string[] nodeTexts)
        {
            if (addonSpec.TextNodeOffsets != null && addonSpec.TextNodeOffsets.Length > 0)
            {
                return TryReadDirectTextNodeTexts(addonAddress, addonSpec.TextNodeOffsets, out nodeTexts);
            }

            return TryReadTalkBubbleNodeTexts(addonAddress, addonSpec, out nodeTexts);
        }

        private bool TryReadDirectTextNodeTexts(
            IntPtr addonAddress,
            long[] textNodeOffsets,
            out string[] nodeTexts)
        {
            var textCandidates = new List<string>();

            foreach (var textNodeOffset in textNodeOffsets)
            {
                var textNodeAddress = _memoryHandler.ReadPointer(addonAddress, textNodeOffset);
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

        private bool TryReadTalkBubbleNodeTexts(
            IntPtr addonAddress,
            AddonRealtimeTextSpec addonSpec,
            out string[] nodeTexts)
        {
            var textCandidates = new List<string>();

            if (addonSpec.TalkBubbleEntriesOffset < 0 ||
                addonSpec.TalkBubbleEntrySize <= 0 ||
                addonSpec.TalkBubbleTextNodeOffset < 0 ||
                addonSpec.TalkBubbleEntryCount <= 0)
            {
                nodeTexts = Array.Empty<string>();
                return false;
            }

            var talkBubblesAddress = AddAddress(addonAddress, addonSpec.TalkBubbleEntriesOffset);
            if (talkBubblesAddress == IntPtr.Zero)
            {
                nodeTexts = Array.Empty<string>();
                return false;
            }

            for (var i = 0; i < addonSpec.TalkBubbleEntryCount; i++)
            {
                var talkBubbleAddress = AddAddress(talkBubblesAddress, (long)i * addonSpec.TalkBubbleEntrySize);
                if (talkBubbleAddress == IntPtr.Zero)
                {
                    continue;
                }

                var textNodeAddress = _memoryHandler.ReadPointer(talkBubbleAddress, addonSpec.TalkBubbleTextNodeOffset);
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
            var raptureAtkModuleOffset = ResolveFieldOffset(uiModuleType, "RaptureAtkModule");
            var lastTalkNameOffset = ResolveFieldOffset(uiModuleType, "LastTalkName");
            var lastTalkTextOffset = ResolveFieldOffset(uiModuleType, "LastTalkText");

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
            var addonSpecs = ResolveAddonSpecs(addonTalkType);

            if (raptureLogModuleOffset < 0 ||
                raptureAtkModuleOffset < 0 ||
                lastTalkNameOffset < 0 ||
                lastTalkTextOffset < 0 ||
                atkUnitManagerOffset < 0 ||
                allLoadedUnitsListOffset < 0 ||
                atkUnitListEntriesOffset < 0 ||
                atkUnitListCountOffset < 0 ||
                atkUnitBaseNameOffset < 0 ||
                atkUnitBaseNameLength <= 0 ||
                atkTextNodeNodeTextOffset < 0 ||
                addonSpecs.Length == 0)
            {
                return UiDirectDialogOffsets.Empty;
            }

            return new UiDirectDialogOffsets(
                raptureLogModuleOffset,
                raptureAtkModuleOffset,
                lastTalkNameOffset,
                lastTalkTextOffset,
                atkUnitManagerOffset,
                allLoadedUnitsListOffset,
                atkUnitListEntriesOffset,
                atkUnitListCountOffset,
                atkUnitBaseNameOffset,
                atkUnitBaseNameLength,
                atkTextNodeNodeTextOffset,
                addonSpecs);
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

        private static AddonRealtimeTextSpec[] ResolveAddonSpecs(Type addonTalkType)
        {
            var addonSpecs = new List<AddonRealtimeTextSpec>();
            var addonTalkTextNodeOffsets = ResolveAddonTalkTextNodeOffsets(addonTalkType);
            if (addonTalkTextNodeOffsets.Length > 0)
            {
                addonSpecs.Add(AddonRealtimeTextSpec.Direct(
                    TalkAddonName,
                    DirectDialogCode,
                    addonTalkTextNodeOffsets,
                    true));
            }

            var miniTalkSpec = ResolveMiniTalkAddonSpec(MiniTalkAddonName);
            if (miniTalkSpec != null)
            {
                addonSpecs.Add(miniTalkSpec);
            }

            var alternateMiniTalkSpec = ResolveMiniTalkAddonSpec(AlternateMiniTalkAddonName);
            if (alternateMiniTalkSpec != null)
            {
                addonSpecs.Add(alternateMiniTalkSpec);
            }

            return addonSpecs.ToArray();
        }

        private static AddonRealtimeTextSpec ResolveMiniTalkAddonSpec(string addonName)
        {
            var addonMiniTalkType = Type.GetType(UiNamespace + "AddonMiniTalk, Sharlayan");
            var talkBubbleEntryType = Type.GetType(UiNamespace + "AddonMiniTalk+TalkBubbleEntry, Sharlayan");
            if (addonMiniTalkType == null || talkBubbleEntryType == null)
            {
                return null;
            }

            var talkBubbleEntriesOffset = ResolveFieldOffset(addonMiniTalkType, "_talkBubbles");
            var talkBubbleEntrySize = Marshal.SizeOf(talkBubbleEntryType);
            var talkBubbleTextNodeOffset = ResolveFieldOffset(talkBubbleEntryType, "BubbleTextNode");
            var talkBubbleEntryCount = ResolveFixedBufferLength(addonMiniTalkType, "_talkBubbles");

            if (talkBubbleEntriesOffset < 0 ||
                talkBubbleEntrySize <= 0 ||
                talkBubbleTextNodeOffset < 0 ||
                talkBubbleEntryCount <= 0)
            {
                return null;
            }

            return AddonRealtimeTextSpec.TalkBubbles(
                addonName,
                CutsceneDialogCode,
                talkBubbleEntriesOffset,
                talkBubbleEntrySize,
                talkBubbleTextNodeOffset,
                talkBubbleEntryCount);
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

        private readonly struct LoadedAddon
        {
            public LoadedAddon(IntPtr addonAddress, string addonName)
            {
                AddonAddress = addonAddress;
                AddonName = addonName ?? string.Empty;
            }

            public IntPtr AddonAddress { get; }
            public string AddonName { get; }
        }

        private readonly struct UiDirectDialogOffsets
        {
            public static UiDirectDialogOffsets Empty =>
                new UiDirectDialogOffsets(-1, -1, -1, -1, -1, -1, -1, -1, -1, 0, -1,
                    Array.Empty<AddonRealtimeTextSpec>());

            public long RaptureLogModuleOffset { get; }
            public long RaptureAtkModuleOffset { get; }
            public long LastTalkNameOffset { get; }
            public long LastTalkTextOffset { get; }
            public long AtkUnitManagerOffset { get; }
            public long AllLoadedUnitsListOffset { get; }
            public long AtkUnitListEntriesOffset { get; }
            public long AtkUnitListCountOffset { get; }
            public long AtkUnitBaseNameOffset { get; }
            public int AtkUnitBaseNameLength { get; }
            public long AtkTextNodeNodeTextOffset { get; }
            public AddonRealtimeTextSpec[] AddonSpecs { get; }

            public bool IsValid =>
                RaptureLogModuleOffset >= 0 &&
                RaptureAtkModuleOffset >= 0 &&
                LastTalkNameOffset >= 0 &&
                LastTalkTextOffset >= 0 &&
                AtkUnitManagerOffset >= 0 &&
                AllLoadedUnitsListOffset >= 0 &&
                AtkUnitListEntriesOffset >= 0 &&
                AtkUnitListCountOffset >= 0 &&
                AtkUnitBaseNameOffset >= 0 &&
                AtkUnitBaseNameLength > 0 &&
                AtkTextNodeNodeTextOffset >= 0 &&
                AddonSpecs != null &&
                AddonSpecs.Length > 0;

            public UiDirectDialogOffsets(
                long raptureLogModuleOffset,
                long raptureAtkModuleOffset,
                long lastTalkNameOffset,
                long lastTalkTextOffset,
                long atkUnitManagerOffset,
                long allLoadedUnitsListOffset,
                long atkUnitListEntriesOffset,
                long atkUnitListCountOffset,
                long atkUnitBaseNameOffset,
                int atkUnitBaseNameLength,
                long atkTextNodeNodeTextOffset,
                AddonRealtimeTextSpec[] addonSpecs)
            {
                RaptureLogModuleOffset = raptureLogModuleOffset;
                RaptureAtkModuleOffset = raptureAtkModuleOffset;
                LastTalkNameOffset = lastTalkNameOffset;
                LastTalkTextOffset = lastTalkTextOffset;
                AtkUnitManagerOffset = atkUnitManagerOffset;
                AllLoadedUnitsListOffset = allLoadedUnitsListOffset;
                AtkUnitListEntriesOffset = atkUnitListEntriesOffset;
                AtkUnitListCountOffset = atkUnitListCountOffset;
                AtkUnitBaseNameOffset = atkUnitBaseNameOffset;
                AtkUnitBaseNameLength = atkUnitBaseNameLength;
                AtkTextNodeNodeTextOffset = atkTextNodeNodeTextOffset;
                AddonSpecs = addonSpecs ?? Array.Empty<AddonRealtimeTextSpec>();
            }
        }

        private sealed class AddonRealtimeTextSpec
        {
            private AddonRealtimeTextSpec(
                string addonName,
                string chatCode,
                long[] textNodeOffsets,
                bool allowNodeSpeaker,
                long talkBubbleEntriesOffset,
                int talkBubbleEntrySize,
                long talkBubbleTextNodeOffset,
                int talkBubbleEntryCount)
            {
                AddonName = addonName;
                ChatCode = chatCode;
                TextNodeOffsets = textNodeOffsets ?? Array.Empty<long>();
                AllowNodeSpeaker = allowNodeSpeaker;
                TalkBubbleEntriesOffset = talkBubbleEntriesOffset;
                TalkBubbleEntrySize = talkBubbleEntrySize;
                TalkBubbleTextNodeOffset = talkBubbleTextNodeOffset;
                TalkBubbleEntryCount = talkBubbleEntryCount;
            }

            public string AddonName { get; }
            public string ChatCode { get; }
            public long[] TextNodeOffsets { get; }
            public bool AllowNodeSpeaker { get; }
            public long TalkBubbleEntriesOffset { get; }
            public int TalkBubbleEntrySize { get; }
            public long TalkBubbleTextNodeOffset { get; }
            public int TalkBubbleEntryCount { get; }

            public static AddonRealtimeTextSpec Direct(
                string addonName,
                string chatCode,
                long[] textNodeOffsets,
                bool allowNodeSpeaker)
            {
                return new AddonRealtimeTextSpec(addonName, chatCode, textNodeOffsets, allowNodeSpeaker, -1, 0, -1, 0);
            }

            public static AddonRealtimeTextSpec TalkBubbles(
                string addonName,
                string chatCode,
                long talkBubbleEntriesOffset,
                int talkBubbleEntrySize,
                long talkBubbleTextNodeOffset,
                int talkBubbleEntryCount)
            {
                return new AddonRealtimeTextSpec(
                    addonName,
                    chatCode,
                    Array.Empty<long>(),
                    false,
                    talkBubbleEntriesOffset,
                    talkBubbleEntrySize,
                    talkBubbleTextNodeOffset,
                    talkBubbleEntryCount);
            }
        }
    }

    internal readonly struct TalkAddonRealtimeDialogSnapshot
    {
        private const string DirectDialogCode = "003D";

        public bool SourceAvailable { get; }
        public string ChatCode { get; }
        public string SpeakerName { get; }
        public string TalkText { get; }

        private TalkAddonRealtimeDialogSnapshot(bool sourceAvailable, string chatCode, string speakerName,
            string talkText)
        {
            SourceAvailable = sourceAvailable;
            ChatCode = chatCode ?? string.Empty;
            SpeakerName = speakerName ?? string.Empty;
            TalkText = talkText ?? string.Empty;
        }

        public static TalkAddonRealtimeDialogSnapshot Unavailable()
        {
            return new TalkAddonRealtimeDialogSnapshot(false, string.Empty, string.Empty, string.Empty);
        }

        public static TalkAddonRealtimeDialogSnapshot Available(string talkText)
        {
            return Available(DirectDialogCode, string.Empty, talkText);
        }

        public static TalkAddonRealtimeDialogSnapshot Available(string chatCode, string speakerName, string talkText)
        {
            return new TalkAddonRealtimeDialogSnapshot(true, chatCode, speakerName, talkText);
        }
    }
}