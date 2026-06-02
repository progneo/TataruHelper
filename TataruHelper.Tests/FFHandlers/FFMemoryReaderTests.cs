using System.Collections.Concurrent;
using System.Linq;
using System.Reflection;

using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;

using NUnit.Framework;

using Sharlayan.Core;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;

namespace TataruHelper.Tests
{
    public class FFMemoryReaderTests
    {
        [Test]
        public void ProcessReadResult_KeepsLogMessagesAndAddsRealtimeMessages()
        {
            var gateway = new FakeGameMemoryGateway
            {
                DirectDialogResult = BuildResult(
                    new ChatLogItem { Code = "003D", Line = "FallbackNpc:DelayedFallback" },
                    new ChatLogItem { Code = "F03D", Line = "RealtimeNpc:RealtimeDialog" },
                    new ChatLogItem { Code = "F044", Line = "RealtimeCutscene" })
            };
            var reader = new FFMemoryReader(gateway, new NullLogger(), new FakeSettingsStore());

            InvokeProcessReadResult(
                reader,
                BuildResult(
                    new ChatLogItem { Code = "003D", Line = "LogNpc:DelayedDialog" },
                    new ChatLogItem { Code = "0044", Line = "DelayedCutscene" }));

            var messages = ReadQueuedMessages(reader);

            Assert.That(messages.Select(message => message.Code),
                Is.EquivalentTo(new[] { "003D", "0044", "F03D", "F044" }));
            Assert.That(messages.Any(message => message.Code == "003D" && message.Text == "LogNpc:DelayedDialog"),
                Is.True);
            Assert.That(messages.Any(message => message.Code == "0044" && message.Text == "DelayedCutscene"), Is.True);
            Assert.That(messages.Any(message => message.Code == "F03D" && message.Text == "RealtimeNpc:RealtimeDialog"),
                Is.True);
            Assert.That(messages.Any(message => message.Code == "F044" && message.Text == "RealtimeCutscene"), Is.True);
            Assert.That(messages.Any(message => message.Text == "FallbackNpc:DelayedFallback"), Is.False);
        }

        [Test]
        public void ProcessReadResult_AlwaysReadsRealtimeGateway()
        {
            var gateway = new FakeGameMemoryGateway
            {
                DirectDialogResult =
                    BuildResult(new ChatLogItem { Code = "F03D", Line = "RealtimeNpc:RealtimeDialog" })
            };
            var reader = new FFMemoryReader(gateway, new NullLogger(), new FakeSettingsStore());

            InvokeProcessReadResult(
                reader,
                BuildResult(new ChatLogItem { Code = "003D", Line = "LogNpc:DelayedDialog" }));

            var messages = ReadQueuedMessages(reader);

            Assert.That(gateway.GetDirectDialogCalls, Is.EqualTo(1));
            Assert.That(messages.Select(message => message.Code), Is.EquivalentTo(new[] { "003D", "F03D" }));
            Assert.That(messages.Any(message => message.Code == "003D" && message.Text == "LogNpc:DelayedDialog"),
                Is.True);
            Assert.That(messages.Any(message => message.Code == "F03D" && message.Text == "RealtimeNpc:RealtimeDialog"),
                Is.True);
        }

        [TestCase("F03D", true)]
        [TestCase("F044", true)]
        [TestCase("003D", false)]
        [TestCase("0044", false)]
        public void IsRealtimeDirectDialogCode_MatchesOnlySyntheticRealtimeCodes(string code, bool expected)
        {
            Assert.That(
                FFMemoryReader.IsRealtimeDirectDialogCode(new ChatLogItem { Code = code }),
                Is.EqualTo(expected));
        }

        private static void InvokeProcessReadResult(FFMemoryReader reader, ChatLogResult result)
        {
            var method = typeof(FFMemoryReader).GetMethod(
                "ProcessReadResult",
                BindingFlags.Instance | BindingFlags.NonPublic);

            method.Invoke(reader, new object[] { result });
        }

        private static FFChatMsg[] ReadQueuedMessages(FFMemoryReader reader)
        {
            var field = typeof(FFMemoryReader).GetField(
                "_ffxivChat",
                BindingFlags.Instance | BindingFlags.NonPublic);

            return ((ConcurrentQueue<FFChatMsg>)field.GetValue(reader)).ToArray();
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

        private sealed class FakeGameMemoryGateway : IGameMemoryGateway
        {
            public int GetDirectDialogCalls { get; private set; }
            public ChatLogResult DirectDialogResult { get; set; } = new ChatLogResult();

            public void SetProcess(
                ProcessModel processModel,
                string gameLanguage,
                string patchVersion,
                bool useLocalCache,
                bool scanAllMemoryRegions)
            {
            }

            public void UnsetProcess()
            {
            }

            public ChatLogResult GetChatLog(int previousArrayIndex, int previousOffset)
            {
                return new ChatLogResult();
            }

            public ChatLogResult GetDirectDialog()
            {
                GetDirectDialogCalls++;
                return DirectDialogResult;
            }

            public bool CheckChatEquality(ChatLogItem item1, ChatLogItem item2)
            {
                return false;
            }
        }

        private sealed class FakeSettingsStore : ISettingsStore
        {
            public string ChatCodesFilePath => string.Empty;
            public string BlackListPath => string.Empty;
            public string IgnoreNickNameChatCodesPath => string.Empty;
            public string SystemSettingsPath => string.Empty;
            public string SettingsPath => string.Empty;
            public string OldSettingsPath => string.Empty;
            public int SettingsSaveDelayMs => 1;
            public int LookForProcessDelayMs => 1;
            public int MemoryReaderDelayMs => 1;
            public int AutoHideWatcherDelayMs => 1;
            public int TranslatorWaitTimeMs => 1;
            public int MaxTranslateTryCount => 1;

            public bool LoadGlobalSettings(string fileName)
            {
                return true;
            }

            public void SaveGlobalSettings(string fileName)
            {
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