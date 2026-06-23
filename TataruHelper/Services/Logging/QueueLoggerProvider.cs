using System;
using System.Globalization;
using System.Text;

using Microsoft.Extensions.Logging;

namespace FFXIVTataruHelper.Services.Logging
{
    public sealed class QueueLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName)
        {
            return new QueueLogger(categoryName);
        }

        public void Dispose()
        {
        }

        private sealed class QueueLogger : ILogger
        {
            private readonly string _categoryName;

            public QueueLogger(string categoryName)
            {
                _categoryName = categoryName;
            }

            public IDisposable BeginScope<TState>(TState state)
            {
                return null;
            }

            public bool IsEnabled(LogLevel logLevel)
            {
                return logLevel != LogLevel.None;
            }

            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception,
                Func<TState, Exception, string> formatter)
            {
                if (!IsEnabled(logLevel))
                    return;

                var builder = new StringBuilder();
                builder.AppendLine(DateTime.Now.ToString(CultureInfo.InvariantCulture));
                builder.Append('[').Append(logLevel).Append("] ").AppendLine(_categoryName);
                builder.AppendLine(formatter(state, exception));

                if (exception != null)
                    builder.AppendLine(exception.ToString());

                Logger.LogQueue.Enqueue(builder.ToString());
                Logger.QueueSignal.Set();
            }
        }
    }
}