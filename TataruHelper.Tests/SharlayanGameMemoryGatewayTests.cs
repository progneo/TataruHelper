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
            var gateway = new SharlayanGameMemoryGateway(directDialogReader, new NullLogger());

            var dialog = gateway.GetDirectDialog();
            var equal = gateway.CheckChatEquality(new ChatLogItem(), new ChatLogItem());

            Assert.That(directDialogReader.ExtractCalls, Is.EqualTo(1));
            Assert.That(directDialogReader.EqualityCalls, Is.EqualTo(1));
            Assert.That(dialog, Is.SameAs(directDialogReader.DirectDialogResult));
            Assert.That(equal, Is.True);
        }

        private sealed class FakeDirectDialogReader : IDirectDialogReader
        {
            public int ExtractCalls { get; private set; }
            public int EqualityCalls { get; private set; }
            public ChatLogResult DirectDialogResult { get; } = new ChatLogResult();

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
