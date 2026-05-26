namespace Translation.OutgoingChat
{
    public interface IChannelPrefixFormatter
    {
        /// <summary>
        /// Returns the FFXIV slash-command prefix (with a trailing space) for the given channel,
        /// or an empty string if no prefix should be applied.
        /// </summary>
        /// <param name="channel">Target channel.</param>
        /// <param name="tellTarget">For <see cref="ChatChannel.Tell"/>, the recipient as "Firstname Lastname" or "Firstname Lastname@World".</param>
        string FormatPrefix(ChatChannel channel, string tellTarget);
    }
}