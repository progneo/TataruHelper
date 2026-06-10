using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Translation.Settings;

namespace Translation.Http
{
    internal static class TranslationHttpPolicy
    {
        public static async Task<string> ExecuteTranslationWithRetryAsync(
            Func<Task<string>> translate,
            TranslationSettings settings,
            ILogger logger,
            string operationName,
            CancellationToken cancellationToken)
        {
            if (translate == null)
                return String.Empty;

            settings = settings ?? new TranslationSettings();
            int maxAttempts = Math.Max(1, settings.TranslationRetryCount);
            int delayMs = Math.Max(0, settings.TranslationRetryDelayMilliseconds);
            string lastResult = String.Empty;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lastResult = await translate().ConfigureAwait(false) ?? String.Empty;
                if (lastResult.Length > 0)
                    return lastResult;

                if (attempt >= maxAttempts)
                    break;

                logger?.LogInformation("{Message}",
                    $"{operationName}: empty translation result, retry {attempt}/{maxAttempts - 1}.");

                if (delayMs > 0)
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            return lastResult;
        }

        public static async Task<string> ExecuteHttpRequestWithRetryAsync(
            Func<Task<string>> request,
            TranslationSettings settings,
            ILogger logger,
            string operationName,
            CancellationToken cancellationToken)
        {
            if (request == null)
                return null;

            settings = settings ?? new TranslationSettings();
            int maxAttempts = Math.Max(1, settings.HttpRequestRetryCount);
            int delayMs = Math.Max(0, settings.HttpRequestRetryDelayMilliseconds);

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                try
                {
                    return await request().ConfigureAwait(false);
                }
                catch (Exception ex) when (IsTransientHttp(ex, cancellationToken))
                {
                    if (attempt >= maxAttempts)
                    {
                        logger?.LogInformation("{Message}",
                            $"{operationName}: HTTP request failed after {maxAttempts} attempts: {ex.Message}");
                        return null;
                    }

                    logger?.LogInformation("{Message}",
                        $"{operationName}: transient HTTP failure, retry {attempt}/{maxAttempts - 1}.");
                }

                if (delayMs > 0)
                    await Task.Delay(delayMs, cancellationToken).ConfigureAwait(false);
            }

            return null;
        }

        private static bool IsTransientHttp(Exception exception, CancellationToken cancellationToken)
        {
            if (exception is HttpRequestException)
                return true;

            // HttpClient timeout surfaces as TaskCanceledException without the caller's token being canceled.
            return exception is TaskCanceledException && !cancellationToken.IsCancellationRequested;
        }
    }
}