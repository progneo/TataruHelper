using System;
using System.Net;
using System.Threading;

using HttpUtilities;

using ILog = Translation.Abstractions.ILog;

namespace Translation.Http
{
    internal static class TranslationHttpPolicy
    {
        public static void ConfigureReader(HttpReader reader)
        {
            if (reader == null)
                return;

            reader.TimeoutMilliseconds = GlobalTranslationSettings.HttpRequestTimeoutMilliseconds;
            reader.ReadWriteTimeoutMilliseconds = GlobalTranslationSettings.HttpReadWriteTimeoutMilliseconds;
        }

        public static HttpResponse ExecuteHttpRequestWithRetry(
            Func<HttpResponse> request,
            ILog logger,
            string operationName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (request == null)
                return new HttpResponse(false, null);

            int maxAttempts = Math.Max(1, GlobalTranslationSettings.HttpRequestRetryCount);
            int delayMs = Math.Max(0, GlobalTranslationSettings.HttpRequestRetryDelayMilliseconds);
            HttpResponse lastResponse = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lastResponse = request();
                if (lastResponse != null && lastResponse.IsSuccessful)
                    return lastResponse;

                bool shouldRetry = attempt < maxAttempts && IsTransient(lastResponse?.InnerException);
                if (!shouldRetry)
                    break;

                logger?.WriteLog($"{operationName}: transient HTTP failure, retry {attempt}/{maxAttempts - 1}.");
                DelayWithCancellation(delayMs, cancellationToken);
            }

            return lastResponse ?? new HttpResponse(false, null);
        }

        public static string ExecuteTranslationWithRetry(
            Func<string> translate,
            ILog logger,
            string operationName,
            CancellationToken cancellationToken = default(CancellationToken))
        {
            if (translate == null)
                return String.Empty;

            int maxAttempts = Math.Max(1, GlobalTranslationSettings.TranslationRetryCount);
            int delayMs = Math.Max(0, GlobalTranslationSettings.TranslationRetryDelayMilliseconds);
            string lastResult = String.Empty;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                lastResult = translate() ?? String.Empty;
                if (lastResult.Length > 0)
                    return lastResult;

                if (attempt >= maxAttempts)
                    break;

                logger?.WriteLog($"{operationName}: empty translation result, retry {attempt}/{maxAttempts - 1}.");
                DelayWithCancellation(delayMs, cancellationToken);
            }

            return lastResult;
        }

        private static void DelayWithCancellation(int delayMs, CancellationToken cancellationToken)
        {
            if (delayMs <= 0)
                return;

            if (cancellationToken == default(CancellationToken))
            {
                Thread.Sleep(delayMs);
                return;
            }

            if (cancellationToken.WaitHandle.WaitOne(delayMs))
                cancellationToken.ThrowIfCancellationRequested();
        }

        private static bool IsTransient(Exception exception)
        {
            var webException = exception as WebException;
            if (webException == null)
                return false;

            switch (webException.Status)
            {
                case WebExceptionStatus.Timeout:
                case WebExceptionStatus.ConnectFailure:
                case WebExceptionStatus.NameResolutionFailure:
                case WebExceptionStatus.ProxyNameResolutionFailure:
                case WebExceptionStatus.KeepAliveFailure:
                case WebExceptionStatus.ReceiveFailure:
                case WebExceptionStatus.SendFailure:
                case WebExceptionStatus.ConnectionClosed:
                    return true;
                default:
                    return false;
            }
        }
    }
}