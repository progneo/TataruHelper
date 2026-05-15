using System.Linq;
using FFXIVTataruHelper.Services.GameMemory;
using NUnit.Framework;
using Sharlayan.Core;
using Sharlayan.Models.ReadResults;

namespace TataruHelper.Tests
{
    public class HeuristicDirectDialogReaderTests
    {
        [Test]
        public void ExtractDirectDialog_ReturnsDialogPanelCandidate()
        {
            var reader = new HeuristicDirectDialogReader();
            var chatLog = new ChatLogResult();
            chatLog.ChatLogItems.Enqueue(new ChatLogItem { Code = "003D", Line = "NPC:Hello there" });

            var direct = reader.ExtractDirectDialog(chatLog);

            Assert.That(direct.ChatLogItems.Count, Is.EqualTo(1));
            Assert.That(direct.ChatLogItems.First().Code, Is.EqualTo("003D"));
        }

        [Test]
        public void CheckChatEquality_IgnoresSpeakerPrefix()
        {
            var reader = new HeuristicDirectDialogReader();
            var first = new ChatLogItem { Line = "NpcName:Welcome" };
            var second = new ChatLogItem { Line = "Other:Welcome" };

            var result = reader.CheckChatEquality(first, second);

            Assert.That(result, Is.True);
        }
    }
}
