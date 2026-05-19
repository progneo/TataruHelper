using System;
using System.Collections.Generic;
using System.Linq;

using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.Logging;

using NUnit.Framework;

using Sharlayan.Core;
using Sharlayan.Models.ReadResults;

namespace TataruHelper.Tests
{
    public class SharlayanGameMemoryGatewayTests
    {
        [Test]
        public void Gateway_DelegatesDirectDialogAndEqualityToReader()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var gateway = CreateGateway(directDialogReader, () => TalkAddonRealtimeDialogSnapshot.Unavailable());

            var dialog = gateway.GetDirectDialog();
            var equal = gateway.CheckChatEquality(new ChatLogItem(), new ChatLogItem());

            Assert.That(directDialogReader.ExtractCalls, Is.EqualTo(1));
            Assert.That(directDialogReader.EqualityCalls, Is.EqualTo(1));
            Assert.That(dialog, Is.SameAs(directDialogReader.DirectDialogResult));
            Assert.That(equal, Is.True);
        }

        [Test]
        public void Gateway_PrioritizesRealtime003D_AndKeepsOnlyFallback0044()
        {
            var directDialogReader = new FakeDirectDialogReader
            {
                DirectDialogResult = BuildResult(
                    new ChatLogItem { Code = "003D", Line = "OldNpc:FromChatLog" },
                    new ChatLogItem { Code = "0044", Line = "CutsceneNpc:FromChatLog" })
            };

            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("003D", string.Empty, "LiveText"));

            var result = gateway.GetDirectDialog();
            var items = result.ChatLogItems.ToArray();

            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items.Count(item => item.Code == "003D"), Is.EqualTo(1));
            Assert.That(items.Any(item => item.Code == "003D" && item.Line == "LiveText"), Is.True);
            Assert.That(items.Any(item => item.Code == "0044" && item.Line == "CutsceneNpc:FromChatLog"), Is.True);
            Assert.That(items.Any(item => item.Code == "003D" && item.Line == "OldNpc:FromChatLog"), Is.False);
        }

        [Test]
        public void Gateway_FallsBackToHeuristicDirectDialog_WhenRealtimeUnavailable()
        {
            var directDialogReader = new FakeDirectDialogReader
            {
                DirectDialogResult = BuildResult(
                    new ChatLogItem { Code = "003D", Line = "FallbackNpc:FallbackText" },
                    new ChatLogItem { Code = "0044", Line = "FallbackCutscene:FallbackText" })
            };

            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Unavailable());

            var result = gateway.GetDirectDialog();
            var items = result.ChatLogItems.ToArray();

            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items.Any(item => item.Code == "003D" && item.Line == "FallbackNpc:FallbackText"), Is.True);
            Assert.That(items.Any(item => item.Code == "0044" && item.Line == "FallbackCutscene:FallbackText"),
                Is.True);
        }

        [Test]
        public void Gateway_DoesNotEmitRealtime003DDuplicatesAcrossTicks()
        {
            var directDialogReader = new FakeDirectDialogReader
            {
                DirectDialogResult = BuildResult(new ChatLogItem { Code = "003D", Line = "ChatlogNpc:ChatlogText" })
            };

            var queue = new Queue<TalkAddonRealtimeDialogSnapshot>();
            queue.Enqueue(TalkAddonRealtimeDialogSnapshot.Available("LiveText"));
            queue.Enqueue(TalkAddonRealtimeDialogSnapshot.Available("LiveText"));

            var gateway = CreateGateway(directDialogReader, () => queue.Dequeue());

            var firstTick = gateway.GetDirectDialog().ChatLogItems.ToArray();
            var secondTick = gateway.GetDirectDialog().ChatLogItems.ToArray();

            Assert.That(firstTick.Length, Is.EqualTo(1));
            Assert.That(firstTick[0].Line, Is.EqualTo("LiveText"));
            Assert.That(secondTick.Length, Is.EqualTo(0));
        }

        [Test]
        public void Gateway_EmitsRealtimeSpeakerPrefix_WhenSpeakerAvailable()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("003D", "LiveNpc", "LiveText"));

            var item = gateway.GetDirectDialog().ChatLogItems.Single();

            Assert.That(item.Code, Is.EqualTo("003D"));
            Assert.That(item.Line, Is.EqualTo("LiveNpc:LiveText"));
        }

        [Test]
        public void Gateway_EmitsRealtime0044_WhenSnapshotIsCutsceneCode()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("0044", "CutsceneNpc", "LiveText"));

            var item = gateway.GetDirectDialog().ChatLogItems.Single();

            Assert.That(item.Code, Is.EqualTo("0044"));
            Assert.That(item.Line, Is.EqualTo("CutsceneNpc:LiveText"));
        }

        [Test]
        public void Gateway_DoesNotSuppressSameRealtimeTextAcrossDifferentCodes()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var queue = new Queue<TalkAddonRealtimeDialogSnapshot>();
            queue.Enqueue(TalkAddonRealtimeDialogSnapshot.Available("003D", string.Empty, "SameText"));
            queue.Enqueue(TalkAddonRealtimeDialogSnapshot.Available("0044", string.Empty, "SameText"));

            var gateway = CreateGateway(directDialogReader, () => queue.Dequeue());

            var firstTick = gateway.GetDirectDialog().ChatLogItems.ToArray();
            var secondTick = gateway.GetDirectDialog().ChatLogItems.ToArray();

            Assert.That(firstTick.Length, Is.EqualTo(1));
            Assert.That(firstTick[0].Code, Is.EqualTo("003D"));
            Assert.That(secondTick.Length, Is.EqualTo(1));
            Assert.That(secondTick[0].Code, Is.EqualTo("0044"));
        }

        [Test]
        public void Gateway_FallsBackToHeuristicDirectDialog_WhenRealtimeAvailableButEmpty()
        {
            var directDialogReader = new FakeDirectDialogReader
            {
                DirectDialogResult = BuildResult(
                    new ChatLogItem { Code = "003D", Line = "FallbackNpc:FallbackText" },
                    new ChatLogItem { Code = "0044", Line = "FallbackCutsceneText" })
            };

            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("0044", "CutsceneNpc", "   "));

            var items = gateway.GetDirectDialog().ChatLogItems.ToArray();

            Assert.That(items.Length, Is.EqualTo(2));
            Assert.That(items.Any(item => item.Code == "003D" && item.Line == "FallbackNpc:FallbackText"), Is.True);
            Assert.That(items.Any(item => item.Code == "0044" && item.Line == "FallbackCutsceneText"), Is.True);
        }

        [Test]
        public void SelectBestTalkText_ReturnsLongestNonEmptyCandidate()
        {
            var result = SharlayanGameMemoryGateway.SelectBestTalkText(new[] { "  ", "short", "the longest line" });
            Assert.That(result, Is.EqualTo("the longest line"));
        }

        [Test]
        public void SelectBestTalkText_ReturnsEmpty_WhenOnlyWhitespaceProvided()
        {
            var result = SharlayanGameMemoryGateway.SelectBestTalkText(new[] { " ", "\t", string.Empty });
            Assert.That(result, Is.EqualTo(string.Empty));
        }

        [Test]
        public void BuildRealtimeSignature_TrimsInput()
        {
            var signature = SharlayanGameMemoryGateway.BuildRealtimeSignature("  Npc:Line  ");
            Assert.That(signature, Is.EqualTo("Npc:Line"));
        }

        [Test]
        public void BuildRealtimeSignature_IncludesChatCodeAndSpeaker()
        {
            var signature = SharlayanGameMemoryGateway.BuildRealtimeSignature(" 0044 ", " Npc ", " Line ");
            Assert.That(signature, Is.EqualTo("0044|Npc|Line"));
        }

        [Test]
        public void BuildRealtimeDialogLine_ReturnsTrimmedTalkText()
        {
            var line = SharlayanGameMemoryGateway.BuildRealtimeDialogLine(
                "  Hello there  ");

            Assert.That(line, Is.EqualTo("Hello there"));
        }

        [Test]
        public void BuildRealtimeDialogLine_ReturnsTalkText_WhenAlreadyNormalized()
        {
            var line = SharlayanGameMemoryGateway.BuildRealtimeDialogLine(
                "Hello there");

            Assert.That(line, Is.EqualTo("Hello there"));
        }

        [Test]
        public void BuildRealtimeDialogLine_ReturnsEmpty_WhenTalkTextIsWhitespace()
        {
            var line = SharlayanGameMemoryGateway.BuildRealtimeDialogLine(
                "   ");

            Assert.That(line, Is.EqualTo(string.Empty));
        }

        [Test]
        public void BuildRealtimeDialogLine_AddsSpeakerPrefix_WhenSpeakerProvided()
        {
            var line = SharlayanGameMemoryGateway.BuildRealtimeDialogLine(
                " LiveNpc ",
                " LiveText ");

            Assert.That(line, Is.EqualTo("LiveNpc:LiveText"));
        }

        private static SharlayanGameMemoryGateway CreateGateway(
            FakeDirectDialogReader directDialogReader,
            Func<TalkAddonRealtimeDialogSnapshot> realtimeReader)
        {
            return new SharlayanGameMemoryGateway(
                directDialogReader,
                new NullLogger(),
                realtimeReader,
                () => new DateTime(2026, 5, 16, 10, 0, 0));
        }

        private static ChatLogResult BuildResult(params ChatLogItem[] items)
        {
            var result = new ChatLogResult();
            foreach (var item in items)
            {
                result.ChatLogItems.Enqueue(item);
            }

            return result;
        }

        private sealed class FakeDirectDialogReader : IDirectDialogReader
        {
            public int ExtractCalls { get; private set; }
            public int EqualityCalls { get; private set; }
            public ChatLogResult DirectDialogResult { get; set; } = new ChatLogResult();

            public ChatLogResult ExtractDirectDialog(ChatLogResult chatLogResult)
            {
                ExtractCalls++;
                return DirectDialogResult;
            }

            public bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2)
            {
                EqualityCalls++;
                return true;
            }
        }

        private sealed class NullLogger : IAppLogger
        {
            public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteConsoleLog(string input) { }
            public void WriteChatLog(string input) { }
        }
    }
}