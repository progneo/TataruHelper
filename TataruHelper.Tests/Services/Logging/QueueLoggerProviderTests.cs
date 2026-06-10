using System;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Logging;

using Microsoft.Extensions.Logging;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.Logging
{
    [TestFixture]
    public class QueueLoggerProviderTests
    {
        [Test]
        public void Log_EnqueuesFormattedMessage_WithCategoryAndLevel()
        {
            while (Logger.LogQueue.TryDequeue(out _))
            {
            }

            using (var provider = new QueueLoggerProvider())
            {
                var logger = provider.CreateLogger("TestCategory");

                logger.LogInformation("{Message}", "hello {with} braces");

                Assert.That(Logger.LogQueue.TryDequeue(out var entry), Is.True);
                Assert.That(entry, Does.Contain("TestCategory"));
                Assert.That(entry, Does.Contain("[Information]"));
                Assert.That(entry, Does.Contain("hello {with} braces"));
            }
        }

        [Test]
        public void Log_IncludesExceptionDetails()
        {
            while (Logger.LogQueue.TryDequeue(out _))
            {
            }

            using (var provider = new QueueLoggerProvider())
            {
                var logger = provider.CreateLogger("TestCategory");

                logger.LogError(new InvalidOperationException("boom"), "operation failed");

                Assert.That(Logger.LogQueue.TryDequeue(out var entry), Is.True);
                Assert.That(entry, Does.Contain("[Error]"));
                Assert.That(entry, Does.Contain("operation failed"));
                Assert.That(entry, Does.Contain("InvalidOperationException"));
                Assert.That(entry, Does.Contain("boom"));
            }
        }
    }
}
