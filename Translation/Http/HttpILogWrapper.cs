using System.Runtime.CompilerServices;

using ILog = Translation.Abstractions.ILog;

namespace Translation.Http
{
    public class HttpILogWrapper : HttpUtilities.ILog
    {
        ILog _Logger = null;

        public HttpILogWrapper(ILog log)
        {
            _Logger = log;
        }

        public void WriteLog(string InputString, [CallerMemberName] string memberName = "",
            [CallerLineNumber] int sourceLineNumber = 0) => _Logger.WriteLog(InputString, memberName, sourceLineNumber);
    }
}