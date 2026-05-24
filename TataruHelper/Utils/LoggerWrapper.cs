using FFXIVTataruHelper.Services.Logging;

using Translation.Abstractions;

namespace FFXIVTataruHelper.Utils
{
    class LoggerWrapper : ILog, Updater.ILog
    {
        private readonly IAppLogger _logger;

        public LoggerWrapper(IAppLogger logger)
        {
            _logger = logger;
        }

        public void WriteLog(string InputString, string memberName = "", int sourceLineNumber = 0)
        {
            _logger.WriteLog(InputString, memberName, sourceLineNumber);
        }
    }
}