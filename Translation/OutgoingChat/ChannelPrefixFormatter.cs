using System;
using System.Text.RegularExpressions;

namespace Translation.OutgoingChat
{
    public sealed class ChannelPrefixFormatter : IChannelPrefixFormatter
    {
        // "Firstname Lastname" with optional "@World" suffix. Letters only (incl. apostrophes/hyphens).
        private static readonly Regex TellTargetPattern = new Regex(
            @"^[A-Za-z'\-]+\s+[A-Za-z'\-]+(?:@[A-Za-z]+)?$",
            RegexOptions.Compiled);

        public string FormatPrefix(ChatChannel channel, string tellTarget)
        {
            switch (channel)
            {
                case ChatChannel.None:
                    return string.Empty;
                case ChatChannel.Say:
                    return "/s ";
                case ChatChannel.Yell:
                    return "/y ";
                case ChatChannel.Shout:
                    return "/sh ";
                case ChatChannel.Party:
                    return "/p ";
                case ChatChannel.Alliance:
                    return "/a ";
                case ChatChannel.FreeCompany:
                    return "/fc ";
                case ChatChannel.NoviceNetwork:
                    return "/n ";
                case ChatChannel.Linkshell1:
                    return "/l1 ";
                case ChatChannel.Linkshell2:
                    return "/l2 ";
                case ChatChannel.Linkshell3:
                    return "/l3 ";
                case ChatChannel.Linkshell4:
                    return "/l4 ";
                case ChatChannel.Linkshell5:
                    return "/l5 ";
                case ChatChannel.Linkshell6:
                    return "/l6 ";
                case ChatChannel.Linkshell7:
                    return "/l7 ";
                case ChatChannel.Linkshell8:
                    return "/l8 ";
                case ChatChannel.CrossWorldLinkshell1:
                    return "/cwl1 ";
                case ChatChannel.CrossWorldLinkshell2:
                    return "/cwl2 ";
                case ChatChannel.CrossWorldLinkshell3:
                    return "/cwl3 ";
                case ChatChannel.CrossWorldLinkshell4:
                    return "/cwl4 ";
                case ChatChannel.CrossWorldLinkshell5:
                    return "/cwl5 ";
                case ChatChannel.CrossWorldLinkshell6:
                    return "/cwl6 ";
                case ChatChannel.CrossWorldLinkshell7:
                    return "/cwl7 ";
                case ChatChannel.CrossWorldLinkshell8:
                    return "/cwl8 ";
                case ChatChannel.Echo:
                    return "/echo ";
                case ChatChannel.Emote:
                    return "/em ";
                case ChatChannel.Tell:
                    var normalizedTarget = (tellTarget ?? string.Empty).Trim();
                    if (normalizedTarget.Length == 0 || !TellTargetPattern.IsMatch(normalizedTarget))
                    {
                        throw new ArgumentException(
                            "Tell target must be 'Firstname Lastname' or 'Firstname Lastname@World'.",
                            nameof(tellTarget));
                    }

                    return "/t " + normalizedTarget + " ";
                default:
                    return string.Empty;
            }
        }
    }
}