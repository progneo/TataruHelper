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
        private const string TalkSubtitleAddonName = "TalkSubtitle";

        /// <summary>
        /// Offset of the subtitle Utf8String inside AddonTalkSubtitle.
        ///
        /// FFXIVClientStructs has no AddonTalkSubtitle type, so unlike every other
        /// addon here this offset cannot be resolved by reflection and was derived
        /// from the running client (verified against 2026.07.16). If cutscene
        /// subtitles stop appearing after a game patch, this is the value to
        /// re-check.
        /// </summary>
        private const long TalkSubtitleTextOffset = 0x238;
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

        private string _lastLoggedLoadedAddons = string.Empty;

        private Dictionary<string, string> _lastAddonText =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private string _stickyCandidateKey;

        private DateTime _lastInlineDiscovery = DateTime.MinValue;

        private static readonly TimeSpan InlineDiscoveryInterval = TimeSpan.FromSeconds(2);

        private const int InlineDiscoveryScanBytes = 0x600;

        private const int MaxCachedAddonNames = 512;

        private readonly Dictionary<IntPtr, string> _addonNameCache = new Dictionary<IntPtr, string>();

        private static bool IsWantedAddonName(string addonName)
        {
            if (string.IsNullOrEmpty(addonName))
            {
                return false;
            }

            return string.Equals(addonName, TalkAddonName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(addonName, TalkSubtitleAddonName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(addonName, MiniTalkAddonName, StringComparison.OrdinalIgnoreCase)
                   || string.Equals(addonName, AlternateMiniTalkAddonName, StringComparison.OrdinalIgnoreCase);
        }

        private TalkAddonRealtimeDialogSnapshot _lastSelectedSnapshot;

        private bool _hasLastSelectedSnapshot;

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

        /// <param name="lastEmittedText">
        /// Text the gateway reported last. The Talk addon holds its line long after
        /// the box is gone, so without this a stale Talk line wins over a cutscene
        /// subtitle that is actually on screen and subtitles never surface.
        /// </param>
        public TalkAddonRealtimeDialogSnapshot TryReadSnapshot(string lastEmittedText = null)
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

            if (!TryReadLoadedAddonSnapshot(atkUnitManagerAddress, lastTalkName, lastTalkText, lastEmittedText,
                    out var snapshot))
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
            string lastEmittedText,
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

            // One read for the whole pointer array instead of one per entry.
            var entryBytes = _memoryHandler.GetByteArray(entriesAddress, safeCount * IntPtr.Size);
            if (entryBytes == null || entryBytes.Length < IntPtr.Size)
            {
                return false;
            }

            var diagnosticNames = Logger.RawDialogLogEnabled ? new List<string>() : null;

            var readable = entryBytes.Length / IntPtr.Size;
            for (var i = 0; i < Math.Min(safeCount, readable); i++)
            {
                var addonAddress = new IntPtr(BitConverter.ToInt64(entryBytes, i * IntPtr.Size));
                if (addonAddress == IntPtr.Zero)
                {
                    continue;
                }

                // Addon objects sit at stable addresses for as long as they live, so
                // their names are cached: re-reading all ~120 of them thirty times a
                // second was the bulk of this reader's cost. Names that matter are
                // re-read below before being acted on, in case an address was freed
                // and handed to a different addon.
                if (!_addonNameCache.TryGetValue(addonAddress, out var addonName))
                {
                    if (!TryReadAddonName(addonAddress, out addonName))
                    {
                        continue;
                    }

                    if (_addonNameCache.Count >= MaxCachedAddonNames)
                    {
                        _addonNameCache.Clear();
                    }

                    _addonNameCache[addonAddress] = addonName;
                }

                // The full roster is what makes it possible to spot a renamed addon
                // after a patch, so it is still gathered when raw logging is on.
                diagnosticNames?.Add(addonName);

                if (!IsWantedAddonName(addonName))
                {
                    continue;
                }

                if (!TryReadAddonName(addonAddress, out var verifiedName) || verifiedName != addonName)
                {
                    _addonNameCache.Remove(addonAddress);
                    if (string.IsNullOrEmpty(verifiedName) || !IsWantedAddonName(verifiedName))
                    {
                        continue;
                    }

                    _addonNameCache[addonAddress] = verifiedName;
                    addonName = verifiedName;
                }

                loadedAddons.Add(new LoadedAddon(addonAddress, addonName));
            }

            if (diagnosticNames != null)
            {
                WriteDistinctRawDialogLog(ref _lastLoggedLoadedAddons,
                    "LoadedAddons=" + string.Join(",", diagnosticNames.OrderBy(n => n)));
            }

            var matchedEmptySource = false;

            // Candidates are ranked after the sweep rather than taking the first
            // with any text: the Talk addon holds its line forever, so first-wins
            // let a finished conversation hide a subtitle that is on screen.
            var candidates = new List<(string Key, TalkAddonRealtimeDialogSnapshot Snapshot, string Text)>();

            foreach (var addonSpec in _uiDirectDialogOffsets.Value.AddonSpecs)
            {
                // A cutscene keeps several TalkSubtitle addons loaded at once and
                // leaves earlier lines sitting in the ones it is not using, so every
                // match has to be considered rather than just the first.
                var matchingAddons = loadedAddons
                    .Where(addon =>
                        string.Equals(addonSpec.AddonName, addon.AddonName, StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                foreach (var loadedAddon in matchingAddons)
                {
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
                    var addonText = SharlayanGameMemoryGateway.NormalizeDialogToken(addonSnapshot.TalkText);
                    if (addonText.Length == 0)
                    {
                        if (!matchedEmptySource)
                        {
                            snapshot = addonSnapshot;
                            matchedEmptySource = true;
                        }

                        continue;
                    }

                    candidates.Add((
                        addonSpec.AddonName + "@" + loadedAddon.AddonAddress.ToInt64().ToString("X"),
                        addonSnapshot,
                        addonText));
                }
            }

            if (TrySelectActiveCandidate(candidates, out snapshot))
            {
                return true;
            }

            return matchedEmptySource;
        }

        /// <summary>
        /// Picks the addon that is actually speaking.
        ///
        /// An addon counts as active when its own text changed since the last poll.
        /// Comparing against the last reported line instead would flip between a
        /// stale Talk line and a live subtitle on alternate polls, re-emitting both
        /// about twenty times a second. When nothing changed the previous choice is
        /// kept, so the signature stays put and nothing is re-emitted.
        /// </summary>
        internal bool TrySelectActiveCandidate(
            IReadOnlyList<(string Key, TalkAddonRealtimeDialogSnapshot Snapshot, string Text)> candidates,
            out TalkAddonRealtimeDialogSnapshot snapshot)
        {
            snapshot = TalkAddonRealtimeDialogSnapshot.Unavailable();

            if (candidates.Count == 0)
            {
                _stickyCandidateKey = null;
                _lastAddonText.Clear();

                // Nothing on screen. Holding the previous line keeps the signature
                // stable rather than letting the UIModule fallback re-announce the
                // conversation that just ended.
                if (_hasLastSelectedSnapshot)
                {
                    snapshot = _lastSelectedSnapshot;
                    return true;
                }

                return false;
            }

            string changedKey = null;
            TalkAddonRealtimeDialogSnapshot changedSnapshot = default;

            // Rebuilt from this sweep so entries for addons the game unloaded do not
            // pile up. Trimming by size instead would make every addon look new
            // again the moment the cap was hit, replaying finished dialogue.
            var seenNow = new Dictionary<string, string>(candidates.Count, StringComparer.Ordinal);

            foreach (var candidate in candidates)
            {
                var isNew = !_lastAddonText.TryGetValue(candidate.Key, out var previous)
                            || !string.Equals(previous, candidate.Text, StringComparison.Ordinal);

                seenNow[candidate.Key] = candidate.Text;

                if (isNew && changedKey == null)
                {
                    changedKey = candidate.Key;
                    changedSnapshot = candidate.Snapshot;
                }
            }

            _lastAddonText = seenNow;

            if (changedKey != null)
            {
                _stickyCandidateKey = changedKey;
                _lastSelectedSnapshot = changedSnapshot;
                _hasLastSelectedSnapshot = true;
                snapshot = changedSnapshot;
                return true;
            }

            if (_stickyCandidateKey != null)
            {
                foreach (var candidate in candidates)
                {
                    if (string.Equals(candidate.Key, _stickyCandidateKey, StringComparison.Ordinal))
                    {
                        snapshot = candidate.Snapshot;
                        return true;
                    }
                }
            }

            // The chosen addon went away - a subtitle clears itself between lines -
            // and nothing else changed. Repeating the previous choice keeps the
            // signature stable so the gap stays silent; falling back to whatever
            // else has text would announce a finished conversation again.
            if (_hasLastSelectedSnapshot)
            {
                snapshot = _lastSelectedSnapshot;
                return true;
            }

            _stickyCandidateKey = candidates[0].Key;
            _lastSelectedSnapshot = candidates[0].Snapshot;
            _hasLastSelectedSnapshot = true;
            snapshot = candidates[0].Snapshot;
            return true;
        }

        internal static TalkAddonRealtimeDialogSnapshot BuildAddonSnapshot(
            string chatCode,
            string[] nodeTexts,
            string lastTalkName,
            string lastTalkText,
            bool allowNodeSpeaker)
        {
            var slots = (nodeTexts ?? Array.Empty<string>())
                .Select(SharlayanGameMemoryGateway.NormalizeDialogToken)
                .ToArray();

            var normalizedNodeTexts = slots.Where(text => text.Length > 0).ToArray();

            var talkText = string.Empty;
            var speakerName = string.Empty;

            // AddonTalk keeps the speaker in its first text node and the line in the
            // second. Picking the longest text instead used to swap them whenever the
            // name happened to be as long as the line - "Short-tempered Thaumaturge"
            // and "Is this our dark stranger?" are both 26 characters.
            if (allowNodeSpeaker && slots.Length >= 2 && slots[1].Length > 0)
            {
                speakerName = slots[0];
                talkText = slots[1];
            }
            else
            {
                talkText = SharlayanGameMemoryGateway.SelectBestTalkText(normalizedNodeTexts);

                if (allowNodeSpeaker)
                {
                    speakerName = normalizedNodeTexts
                        .FirstOrDefault(text =>
                            !string.Equals(text, talkText, StringComparison.Ordinal)
                        ) ?? string.Empty;
                }
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
            if (addonSpec.InlineTextOffset >= 0)
            {
                // The known offset always wins. A discovered one is never latched
                // onto: a single false positive would otherwise keep feeding
                // whatever it landed on for the rest of the session, in place of
                // the subtitles it was supposed to rescue.
                if (TryReadUtf8String(addonAddress, addonSpec.InlineTextOffset, out var inlineText) &&
                    inlineText.Length > 0)
                {
                    nodeTexts = new[] { inlineText };
                    return true;
                }

                // Nothing there. Usually that just means no subtitle is showing, but
                // it is also what a patch moving the field looks like - and this is
                // the one offset FFXIVClientStructs cannot supply.
                if (TryDiscoverInlineTextOffset(addonAddress, out _, out var discoveredText))
                {
                    nodeTexts = new[] { discoveredText };
                    return true;
                }

                nodeTexts = Array.Empty<string>();
                return true;
            }

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

            // Slots stay aligned with textNodeOffsets, empties included: for
            // AddonTalk the first offset is the speaker node and the second the
            // dialogue node, and dropping blanks here would shift that mapping.
            foreach (var textNodeOffset in textNodeOffsets)
            {
                var textNodeAddress = _memoryHandler.ReadPointer(addonAddress, textNodeOffset);
                if (textNodeAddress == IntPtr.Zero)
                {
                    textCandidates.Add(string.Empty);
                    continue;
                }

                if (!TryReadUtf8String(textNodeAddress, _uiDirectDialogOffsets.Value.AtkTextNodeNodeTextOffset,
                        out var candidate))
                {
                    textCandidates.Add(string.Empty);
                    continue;
                }

                textCandidates.Add(SharlayanGameMemoryGateway.NormalizeDialogToken(candidate));
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

        /// <summary>
        /// Looks through the addon for a Utf8String holding readable text, so a
        /// patch that moves the subtitle field is recovered from automatically
        /// instead of silently ending cutscene translation.
        ///
        /// Throttled, because while no subtitle is on screen there is genuinely
        /// nothing to find and the search would otherwise run every poll.
        /// </summary>
        private bool TryDiscoverInlineTextOffset(IntPtr addonAddress, out long offset, out string text)
        {
            offset = -1;
            text = string.Empty;

            var now = DateTime.UtcNow;
            if (now - _lastInlineDiscovery < InlineDiscoveryInterval)
            {
                return false;
            }

            _lastInlineDiscovery = now;

            byte[] block;
            try
            {
                block = _memoryHandler.GetByteArray(addonAddress, InlineDiscoveryScanBytes);
            }
            catch (Exception)
            {
                return false;
            }

            if (block == null || block.Length < (int)Utf8StringInlineBufferOffset)
            {
                return false;
            }

            var bestLength = 0;

            for (var candidate = 0; candidate + (int)Utf8StringInlineBufferOffset <= block.Length; candidate += 8)
            {
                if (!TryParseUtf8StringHeader(block, candidate, out var byteCount, out var isInline,
                        out var dataPointer))
                {
                    continue;
                }

                string value;
                if (isInline)
                {
                    var start = candidate + (int)Utf8StringInlineBufferOffset;
                    var available = Math.Min((int)byteCount, block.Length - start);
                    if (available <= 0)
                    {
                        continue;
                    }

                    value = DecodeUtf8(block, start, available);
                }
                else
                {
                    var data = _memoryHandler.GetByteArray(new IntPtr(dataPointer), (int)byteCount);
                    if (data == null || data.Length == 0)
                    {
                        continue;
                    }

                    value = DecodeUtf8(data, 0, data.Length);
                }

                if (!LooksLikeDialogueText(value) || value.Length <= bestLength)
                {
                    continue;
                }

                bestLength = value.Length;
                offset = candidate;
                text = value;
            }

            if (offset < 0)
            {
                return false;
            }

            if (Logger.RawDialogLogEnabled)
            {
                Logger.WriteRawDialogLog($"Discovered subtitle Utf8String at +0x{offset:X}");
            }

            return true;
        }

        private static string DecodeUtf8(byte[] data, int start, int count)
        {
            var terminator = Array.IndexOf(data, (byte)0, start, count);
            if (terminator >= 0)
            {
                count = terminator - start;
            }

            return count <= 0 ? string.Empty : Encoding.UTF8.GetString(data, start, count);
        }

        /// <summary>
        /// Recognises a Utf8String header inside a block of addon memory.
        ///
        /// Used to rediscover where a string lives when the offset baked into the
        /// code stops working - the layout is distinctive enough to find by shape:
        /// a byte count, a matching capacity, an inline flag, and either an inline
        /// buffer or a pointer.
        /// </summary>
        internal static bool TryParseUtf8StringHeader(byte[] buffer, int offset, out long byteCount,
            out bool isInline, out long dataPointer)
        {
            byteCount = 0;
            isInline = false;
            dataPointer = 0;

            if (buffer == null || offset < 0 || offset + (int)Utf8StringInlineBufferOffset > buffer.Length)
            {
                return false;
            }

            var bufUsed = BitConverter.ToInt64(buffer, offset + (int)Utf8StringBufUsedOffset);
            var length = BitConverter.ToInt64(buffer, offset + (int)Utf8StringLengthOffset);

            byteCount = bufUsed > 0 ? bufUsed : length;
            if (byteCount <= 0 || byteCount > MaxUtf8StringByteLength)
            {
                return false;
            }

            // Capacity has to be able to hold what the length claims.
            if (bufUsed > 0 && length > 0 && length < bufUsed - 1)
            {
                return false;
            }

            isInline = buffer[offset + (int)Utf8StringInlineFlagOffset] != 0;
            dataPointer = BitConverter.ToInt64(buffer, offset + (int)Utf8StringPointerOffset);

            if (!isInline && (dataPointer <= 0x10000 || dataPointer > 0x7FFFFFFFFFFF))
            {
                return false;
            }

            return true;
        }

        /// <summary>
        /// True when the bytes read like a spoken line rather than something else
        /// that happens to decode.
        ///
        /// An addon is full of readable strings that are not dialogue - animation
        /// and layout identifiers such as "cbbp_a_deact", short labels such as
        /// "ПЭ 2" - and a loose test let those reach the translator as if they were
        /// subtitles. A line of dialogue is long, has several words, and does not
        /// look like a code identifier.
        /// </summary>
        internal static bool LooksLikeDialogueText(string value)
        {
            const int minDialogueLength = 12;

            if (string.IsNullOrWhiteSpace(value) || value.Length < minDialogueLength)
            {
                return false;
            }

            if (value.IndexOf('_') >= 0)
            {
                return false;
            }

            var letters = 0;
            var visible = 0;
            var words = 1;

            foreach (var c in value)
            {
                if (char.IsControl(c) && c != '\n' && c != '\r' && c != '\t')
                {
                    return false;
                }

                if (char.IsWhiteSpace(c))
                {
                    words++;
                    continue;
                }

                visible++;

                if (char.IsLetter(c))
                {
                    letters++;
                }
            }

            if (words < 3 || visible == 0)
            {
                return false;
            }

            return letters * 10 >= visible * 7;
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

            addonSpecs.Add(AddonRealtimeTextSpec.InlineText(
                TalkSubtitleAddonName,
                CutsceneDialogCode,
                TalkSubtitleTextOffset));

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
                int talkBubbleEntryCount,
                long inlineTextOffset)
            {
                AddonName = addonName;
                ChatCode = chatCode;
                TextNodeOffsets = textNodeOffsets ?? Array.Empty<long>();
                AllowNodeSpeaker = allowNodeSpeaker;
                TalkBubbleEntriesOffset = talkBubbleEntriesOffset;
                TalkBubbleEntrySize = talkBubbleEntrySize;
                TalkBubbleTextNodeOffset = talkBubbleTextNodeOffset;
                TalkBubbleEntryCount = talkBubbleEntryCount;
                InlineTextOffset = inlineTextOffset;
            }

            public string AddonName { get; }
            public string ChatCode { get; }
            public long[] TextNodeOffsets { get; }
            public bool AllowNodeSpeaker { get; }
            public long TalkBubbleEntriesOffset { get; }
            public int TalkBubbleEntrySize { get; }
            public long TalkBubbleTextNodeOffset { get; }
            public int TalkBubbleEntryCount { get; }

            /// <summary>Offset of a Utf8String stored in the addon itself, or -1.</summary>
            public long InlineTextOffset { get; }

            public static AddonRealtimeTextSpec Direct(
                string addonName,
                string chatCode,
                long[] textNodeOffsets,
                bool allowNodeSpeaker)
            {
                return new AddonRealtimeTextSpec(addonName, chatCode, textNodeOffsets, allowNodeSpeaker, -1, 0, -1, 0,
                    -1);
            }

            /// <summary>
            /// For addons that keep their line in an inline Utf8String rather than
            /// behind an AtkTextNode pointer.
            /// </summary>
            public static AddonRealtimeTextSpec InlineText(
                string addonName,
                string chatCode,
                long inlineTextOffset)
            {
                return new AddonRealtimeTextSpec(addonName, chatCode, Array.Empty<long>(), false, -1, 0, -1, 0,
                    inlineTextOffset);
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
                    talkBubbleEntryCount,
                    -1);
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