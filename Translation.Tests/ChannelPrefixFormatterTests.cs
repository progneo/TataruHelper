using System;

using NUnit.Framework;

using Translation.OutgoingChat;

namespace Translation.Tests
{
    [TestFixture]
    public class ChannelPrefixFormatterTests
    {
        private ChannelPrefixFormatter _formatter;

        [SetUp]
        public void SetUp()
        {
            _formatter = new ChannelPrefixFormatter();
        }

        [TestCase(ChatChannel.None, "")]
        [TestCase(ChatChannel.Say, "/s ")]
        [TestCase(ChatChannel.Yell, "/y ")]
        [TestCase(ChatChannel.Shout, "/sh ")]
        [TestCase(ChatChannel.Party, "/p ")]
        [TestCase(ChatChannel.Alliance, "/a ")]
        [TestCase(ChatChannel.FreeCompany, "/fc ")]
        [TestCase(ChatChannel.NoviceNetwork, "/n ")]
        [TestCase(ChatChannel.Echo, "/echo ")]
        [TestCase(ChatChannel.Emote, "/em ")]
        public void FormatPrefix_ReturnsExpectedPrefix(ChatChannel channel, string expected)
        {
            Assert.That(_formatter.FormatPrefix(channel, null), Is.EqualTo(expected));
        }

        [TestCase(ChatChannel.Linkshell1, "/l1 ")]
        [TestCase(ChatChannel.Linkshell4, "/l4 ")]
        [TestCase(ChatChannel.Linkshell8, "/l8 ")]
        [TestCase(ChatChannel.CrossWorldLinkshell1, "/cwl1 ")]
        [TestCase(ChatChannel.CrossWorldLinkshell5, "/cwl5 ")]
        [TestCase(ChatChannel.CrossWorldLinkshell8, "/cwl8 ")]
        public void FormatPrefix_Linkshells(ChatChannel channel, string expected)
        {
            Assert.That(_formatter.FormatPrefix(channel, null), Is.EqualTo(expected));
        }

        [Test]
        public void FormatPrefix_TellWithoutWorld_ReturnsTellPrefix()
        {
            Assert.That(_formatter.FormatPrefix(ChatChannel.Tell, "Firstname Lastname"),
                Is.EqualTo("/t Firstname Lastname "));
        }

        [Test]
        public void FormatPrefix_TellWithWorld_ReturnsTellPrefixWithWorld()
        {
            Assert.That(_formatter.FormatPrefix(ChatChannel.Tell, "Firstname Lastname@Gilgamesh"),
                Is.EqualTo("/t Firstname Lastname@Gilgamesh "));
        }

        [Test]
        public void FormatPrefix_TellTrimsWhitespace()
        {
            Assert.That(_formatter.FormatPrefix(ChatChannel.Tell, "  Firstname Lastname  "),
                Is.EqualTo("/t Firstname Lastname "));
        }

        [TestCase("")]
        [TestCase("OnlyOne")]
        [TestCase("Bad@World")]
        [TestCase("Firstname Lastname@")]
        [TestCase("Firstname Lastname@World With Space")]
        public void FormatPrefix_TellMalformed_Throws(string target)
        {
            Assert.Throws<ArgumentException>(() => _formatter.FormatPrefix(ChatChannel.Tell, target));
        }
    }
}