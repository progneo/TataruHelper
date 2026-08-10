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
            Assert.That(items.Any(item => item.Code == "003D" && item.Line == "LiveText"), Is.True,
                "the line read off the screen keeps the channel's own code");
            Assert.That(items.Any(item => item.Code == "0044" && item.Line == "CutsceneNpc:FromChatLog"), Is.True);
            Assert.That(items.Any(item => item.Code == "003D" && item.Line == "OldNpc:FromChatLog"), Is.False);
        }

        // What lets the reader tell a channel it can read off the screen from one
        // it cannot, and so whether the chat log's copy is a repeat or the only
        // copy there will be.
        [Test]
        public void Gateway_ReportsWhichCodesItHasManagedToReadLive()
        {
            var gateway = CreateGateway(
                new FakeDirectDialogReader(),
                () => TalkAddonRealtimeDialogSnapshot.Available("003D", string.Empty, "LiveText"));

            Assert.That(gateway.HasReadCodeLive("003D"), Is.False, "nothing read yet");

            gateway.GetDirectDialog();

            Assert.Multiple(() =>
            {
                Assert.That(gateway.HasReadCodeLive("003D"), Is.True);
                Assert.That(gateway.HasReadCodeLive("0044"), Is.False, "no subtitle has been read");
            });
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
        public void Gateway_EmitsTheCutsceneCode_WhenSnapshotIsASubtitle()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("0044", "CutsceneNpc", "LiveText"));

            var item = gateway.GetDirectDialog().ChatLogItems.Single();

            Assert.That(item.Code, Is.EqualTo("0044"));
            Assert.That(item.Line, Is.EqualTo("CutsceneNpc:LiveText"));
        }

        // A cutscene can put the same words in the dialogue box and in the
        // subtitle at once. With the chat code deciding what counted as a new
        // line, those arrived one after the other in the window, in the two
        // different colours the codes are drawn in - the same sentence twice.
        [Test]
        public void Gateway_SuppressesTheSameLineShownByTwoAddonsAtOnce()
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
            Assert.That(secondTick, Is.Empty);
        }

        // Cutscene narration reaches the chat log under 0039, not the codes
        // dialogue usually carries. Requiring one of those meant every line of
        // it was shown twice - once read off the screen, once from the log.
        [Test]
        public void Gateway_DropsTheChatLogCopy_WhateverCodeItArrivesUnder()
        {
            var narration = "...The crackling warmth of Alphinaud's campfire.";
            var directDialogReader = new FakeDirectDialogReader();
            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("003D", string.Empty, narration));

            Assert.That(gateway.GetDirectDialog().ChatLogItems.Single().Line, Is.EqualTo(narration));

            var fromChatLog = BuildResult(new ChatLogItem { Code = "0039", Line = narration });
            gateway.DropLinesAlreadySeenLive(fromChatLog);

            Assert.That(fromChatLog.ChatLogItems, Is.Empty);
        }

        [Test]
        public void Gateway_KeepsAChatLogLineNobodySaidOnScreen()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var gateway = CreateGateway(
                directDialogReader,
                () => TalkAddonRealtimeDialogSnapshot.Available("003D", string.Empty, "Something said aloud"));

            gateway.GetDirectDialog();

            var fromChatLog = BuildResult(new ChatLogItem { Code = "0039", Line = "Something nobody said aloud" });
            gateway.DropLinesAlreadySeenLive(fromChatLog);

            Assert.That(fromChatLog.ChatLogItems, Has.Count.EqualTo(1));
        }

        // Two characters can say the same short thing - "Understood." - and
        // both deserve to be shown.
        [Test]
        public void Gateway_ReportsTheSameWordsFromADifferentSpeaker()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var queue = new Queue<TalkAddonRealtimeDialogSnapshot>();
            queue.Enqueue(TalkAddonRealtimeDialogSnapshot.Available("003D", "Cid", "Understood."));
            queue.Enqueue(TalkAddonRealtimeDialogSnapshot.Available("003D", "Yda", "Understood."));

            var gateway = CreateGateway(directDialogReader, () => queue.Dequeue());

            Assert.That(gateway.GetDirectDialog().ChatLogItems.Single().Line, Is.EqualTo("Cid:Understood."));
            Assert.That(gateway.GetDirectDialog().ChatLogItems.Single().Line, Is.EqualTo("Yda:Understood."));
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
        public void SelectRealtimeSnapshot_PrioritizesAddonTextOverLastTalkText()
        {
            var snapshot = TalkAddonRealtimeReader.SelectRealtimeSnapshot(
                "DelayedNpc",
                "DelayedText",
                new[] { TalkAddonRealtimeDialogSnapshot.Available("003D", string.Empty, "RealtimeAddonText") });

            Assert.That(snapshot.ChatCode, Is.EqualTo("003D"));
            Assert.That(snapshot.SpeakerName, Is.Empty);
            Assert.That(snapshot.TalkText, Is.EqualTo("RealtimeAddonText"));
        }

        [Test]
        public void SelectRealtimeSnapshot_DoesNotUseLastTalkNameWithDifferentMiniTalkAddonText()
        {
            var snapshot = TalkAddonRealtimeReader.SelectRealtimeSnapshot(
                "CutsceneNpc",
                "DelayedText",
                new[] { TalkAddonRealtimeDialogSnapshot.Available("0044", string.Empty, "RealtimeBubbleText") });

            Assert.That(snapshot.ChatCode, Is.EqualTo("0044"));
            Assert.That(snapshot.SpeakerName, Is.Empty);
            Assert.That(snapshot.TalkText, Is.EqualTo("RealtimeBubbleText"));
        }

        [Test]
        public void SelectRealtimeSnapshot_UsesLastTalkName_WhenLastTalkTextMatchesAddonText()
        {
            var snapshot = TalkAddonRealtimeReader.SelectRealtimeSnapshot(
                "CutsceneNpc",
                "RealtimeBubbleText",
                new[] { TalkAddonRealtimeDialogSnapshot.Available("0044", string.Empty, "RealtimeBubbleText") });

            Assert.That(snapshot.ChatCode, Is.EqualTo("0044"));
            Assert.That(snapshot.SpeakerName, Is.EqualTo("CutsceneNpc"));
            Assert.That(snapshot.TalkText, Is.EqualTo("RealtimeBubbleText"));
        }

        [Test]
        public void SelectRealtimeSnapshot_FallsBackToLastTalkText_WhenAddonTextIsEmpty()
        {
            var snapshot = TalkAddonRealtimeReader.SelectRealtimeSnapshot(
                "FallbackNpc",
                "FallbackLastTalkText",
                new[] { TalkAddonRealtimeDialogSnapshot.Available("0044", string.Empty, "   ") });

            Assert.That(snapshot.ChatCode, Is.EqualTo("003D"));
            Assert.That(snapshot.SpeakerName, Is.EqualTo("FallbackNpc"));
            Assert.That(snapshot.TalkText, Is.EqualTo("FallbackLastTalkText"));
        }

        [Test]
        public void BuildAddonSnapshot_SplitsVisibleTalkSpeakerAndBody()
        {
            var snapshot = TalkAddonRealtimeReader.BuildAddonSnapshot(
                "003D",
                new[] { "VisibleNpc", "Visible dialog text" },
                "StaleNpc",
                "Stale dialog text",
                true);

            Assert.That(snapshot.ChatCode, Is.EqualTo("003D"));
            Assert.That(snapshot.SpeakerName, Is.EqualTo("VisibleNpc"));
            Assert.That(snapshot.TalkText, Is.EqualTo("Visible dialog text"));
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

        // What tells one utterance from another is who said it and what they
        // said - not which addon put it on screen.
        [Test]
        public void BuildRealtimeSignature_IsSpeakerAndText()
        {
            Assert.That(SharlayanGameMemoryGateway.BuildRealtimeSignature(" Npc ", " Line "),
                Is.EqualTo("Npc|Line"));
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

        // The first line after attaching used to be swallowed, because the Talk
        // addon holds what was said before the app started and announcing that
        // read as a conversation happening now. The reader skips addons the game
        // is not drawing, so a line that gets this far is one on screen - and
        // holding it back only meant walking up to an NPC right after launch and
        // getting nothing.
        [Test]
        public void Gateway_EmitsTheFirstRealtimeSnapshot_AfterAttaching()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var snapshot = TalkAddonRealtimeDialogSnapshot.Available("003D", "Npc", "The first thing anyone says");
            var gateway = CreateGateway(directDialogReader, () => snapshot);

            gateway.ResetRealtimeDialogState();

            var item = gateway.GetDirectDialog().ChatLogItems.Single();

            Assert.That(item.Line, Is.EqualTo("Npc:The first thing anyone says"));
        }

        [Test]
        public void Gateway_EmitsRealtimeSnapshot_WhenTheLineChanges()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var current = TalkAddonRealtimeDialogSnapshot.Available("003D", "OldNpc", "First line");
            var gateway = CreateGateway(directDialogReader, () => current);

            gateway.ResetRealtimeDialogState();
            gateway.GetDirectDialog();

            current = TalkAddonRealtimeDialogSnapshot.Available("003D", "NewNpc", "Fresh line");

            var item = gateway.GetDirectDialog().ChatLogItems.Single();

            Assert.That(item.Line, Is.EqualTo("NewNpc:Fresh line"));
        }

        // Nothing on screen must clear what was last said, or the same words
        // said again - an NPC repeating a bubble as you walk past - match the
        // signature still held and are taken for an echo.
        [Test]
        public void Gateway_EmitsTheSameLineAgain_AfterTheScreenClears()
        {
            var directDialogReader = new FakeDirectDialogReader();
            var current = TalkAddonRealtimeDialogSnapshot.Available("0044", string.Empty, "The wood... It's watching!");
            var gateway = CreateGateway(directDialogReader, () => current);

            gateway.ResetRealtimeDialogState();
            Assert.That(gateway.GetDirectDialog().ChatLogItems.Single().Line,
                Is.EqualTo("The wood... It's watching!"));

            current = TalkAddonRealtimeDialogSnapshot.Unavailable();
            gateway.GetDirectDialog();

            current = TalkAddonRealtimeDialogSnapshot.Available("0044", string.Empty, "The wood... It's watching!");

            Assert.That(gateway.GetDirectDialog().ChatLogItems.Single().Line,
                Is.EqualTo("The wood... It's watching!"));
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