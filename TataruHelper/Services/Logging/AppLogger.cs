namespace FFXIVTataruHelper.Services.Logging
{
    public sealed class AppLogger : IAppLogger
    {
        public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0)
        {
            Logger.WriteLog(input, memberName, sourceLineNumber);
        }

        public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0)
        {
            Logger.WriteLog(input, memberName, sourceLineNumber);
        }

        public void WriteConsoleLog(string input)
        {
            Logger.WriteConsoleLog(input);
        }

        public void WriteChatLog(string input)
        {
            Logger.WriteChatLog(input);
        }
    }
}
