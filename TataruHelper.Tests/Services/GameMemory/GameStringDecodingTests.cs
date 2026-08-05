using System.Collections.Generic;
using System.Linq;
using System.Text;

using FFXIVTataruHelper.Services.GameMemory;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.GameMemory
{
    // Dialogue carries formatting payloads - italics, highlights, auto-translate -
    // wrapped in 0x02 … 0x03. Nothing strips them on the way to the translator,
    // and the payload's kind byte is printable, so an emphasised line reached the
    // chat window with stray letters glued to both ends.
    [TestFixture]
    public class GameStringDecodingTests
    {
        private static byte[] Bytes(params object[] parts)
        {
            var result = new List<byte>();
            foreach (var part in parts)
            {
                if (part is string text)
                {
                    result.AddRange(Encoding.UTF8.GetBytes(text));
                }
                else
                {
                    result.Add(System.Convert.ToByte(part));
                }
            }

            return result.ToArray();
        }

        private static string Decode(byte[] data)
        {
            return TalkAddonRealtimeReader.DecodeGameString(data, 0, data.Length);
        }

        // Taken byte for byte out of the raw dialogue log: an emphasis payload
        // (kind 0x48) opens the line and another closes it, and the line arrived
        // as "H�#O mournful voice of creation!...H".
        [Test]
        public void EmphasisedLine_LosesItsPayloads()
        {
            var data = Bytes(
                0x02, 0x48, 0x04, 0xFF, 0x02, 0x23, 0x03,
                "O mournful voice of creation! Grant ye this humble stone a soul, that it may wake to life!",
                0x02, 0x48, 0x02, 0x01, 0x03);

            Assert.That(Decode(data), Is.EqualTo(
                "O mournful voice of creation! Grant ye this humble stone a soul, that it may wake to life!"));
        }

        [Test]
        public void PlainLine_IsUntouched()
        {
            Assert.That(Decode(Bytes("Welcome to the Carline Canopy.")),
                Is.EqualTo("Welcome to the Carline Canopy."));
        }

        [Test]
        public void PayloadInTheMiddle_LeavesBothSides()
        {
            var data = Bytes("Seek out ", 0x02, 0x1A, 0x02, 0x02, 0x03, "Minfilia", 0x02, 0x1A, 0x02, 0x01, 0x03,
                " in the Waking Sands.");

            Assert.That(Decode(data), Is.EqualTo("Seek out Minfilia in the Waking Sands."));
        }

        [Test]
        public void NonLatinText_SurvivesIntact()
        {
            var data = Bytes(0x02, 0x48, 0x02, 0x01, 0x03, "光の戦士よ");

            Assert.That(Decode(data), Is.EqualTo("光の戦士よ"));
        }

        // A payload that never closes means the rest of the buffer is not text.
        [Test]
        public void UnterminatedPayload_KeepsWhatCameBefore()
        {
            var data = Bytes("Hold, ", 0x02, 0x48, 0x04, 0xFF, 0xFE, 0xFD);

            Assert.That(Decode(data), Is.EqualTo("Hold, "));
        }

        [Test]
        public void PayloadOnly_YieldsNothing()
        {
            Assert.That(Decode(Bytes(0x02, 0x48, 0x02, 0x01, 0x03)), Is.Empty);
        }

        [Test]
        public void EmptyInput_YieldsNothing()
        {
            Assert.That(TalkAddonRealtimeReader.DecodeGameString(null, 0, 4), Is.Empty);
            Assert.That(TalkAddonRealtimeReader.DecodeGameString(new byte[0], 0, 0), Is.Empty);
        }
    }
}
