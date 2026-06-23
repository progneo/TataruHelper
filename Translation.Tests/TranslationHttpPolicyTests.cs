using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using Translation.Http;
using Translation.Settings;

namespace Translation.Tests
{
    [TestFixture]
    public class TranslationHttpPolicyTests
    {
        [Test]
        public void ExecuteTranslationWithRetryAsync_Throws_WhenTokenIsAlreadyCanceled()
        {
            var cts = new CancellationTokenSource();
            cts.Cancel();

            Assert.ThrowsAsync<OperationCanceledException>(() =>
                TranslationHttpPolicy.ExecuteTranslationWithRetryAsync(
                    () => Task.FromResult(string.Empty),
                    new TranslationSettings(),
                    NullLogger.Instance,
                    "test",
                    cts.Token));
        }

        [Test]
        public async Task ExecuteTranslationWithRetryAsync_StopsAfterSuccessfulAttempt()
        {
            var attempt = 0;

            var result = await TranslationHttpPolicy.ExecuteTranslationWithRetryAsync(
                () =>
                {
                    attempt++;
                    return Task.FromResult(attempt == 2 ? "ok" : string.Empty);
                },
                new TranslationSettings(),
                NullLogger.Instance,
                "test",
                CancellationToken.None);

            Assert.That(result, Is.EqualTo("ok"));
            Assert.That(attempt, Is.EqualTo(2));
        }

        [Test]
        public async Task ExecuteHttpRequestWithRetryAsync_RetriesTransientFailures()
        {
            var attempt = 0;

            var result = await TranslationHttpPolicy.ExecuteHttpRequestWithRetryAsync(
                () =>
                {
                    attempt++;
                    if (attempt == 1)
                        throw new HttpRequestException("transient");

                    return Task.FromResult("body");
                },
                new TranslationSettings(),
                NullLogger.Instance,
                "test",
                CancellationToken.None);

            Assert.That(result, Is.EqualTo("body"));
            Assert.That(attempt, Is.EqualTo(2));
        }

        [Test]
        public void ExecuteHttpRequestWithRetryAsync_DoesNotRetry_NonTransientFailures()
        {
            var attempt = 0;

            Assert.ThrowsAsync<InvalidOperationException>(() =>
                TranslationHttpPolicy.ExecuteHttpRequestWithRetryAsync(
                    () =>
                    {
                        attempt++;
                        throw new InvalidOperationException("hard failure");
                    },
                    new TranslationSettings(),
                    NullLogger.Instance,
                    "test",
                    CancellationToken.None));

            Assert.That(attempt, Is.EqualTo(1));
        }
    }
}