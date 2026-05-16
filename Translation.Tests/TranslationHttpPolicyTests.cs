using NUnit.Framework;
using System;
using System.Threading;
using Translation.HttpUtils;

namespace Translation.Tests
{
    [TestFixture]
    public class TranslationHttpPolicyTests
    {
        [Test]
        public void ExecuteTranslationWithRetry_Throws_WhenTokenIsAlreadyCanceled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.Throws<OperationCanceledException>(() =>
                TranslationHttpPolicy.ExecuteTranslationWithRetry(
                    () => string.Empty,
                    new NullLog(),
                    "test",
                    cts.Token));
        }

        [Test]
        public void ExecuteTranslationWithRetry_StopsAfterSuccessfulAttempt()
        {
            var attempt = 0;

            var result = TranslationHttpPolicy.ExecuteTranslationWithRetry(
                () =>
                {
                    attempt++;
                    return attempt == 2 ? "ok" : string.Empty;
                },
                new NullLog(),
                "test");

            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(attempt, Is.EqualTo(2));
        }

        private sealed class NullLog : ILog
        {
            public void WriteLog(string inputString, string memberName = "", int sourceLineNumber = 0)
            {
            }
        }
    }
}
