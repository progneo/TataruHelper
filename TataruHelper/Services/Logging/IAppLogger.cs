using System.Runtime.CompilerServices;

namespace FFXIVTataruHelper.Services.Logging
{
    public interface IAppLogger
    {
        void WriteLog(string input, string memberName = "", int sourceLineNumber = 0);

        void WriteLog(object input, string memberName = "", int sourceLineNumber = 0);

        void WriteConsoleLog(string input);

        void WriteChatLog(string input);
    }
}
