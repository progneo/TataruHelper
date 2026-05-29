using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Translation.HttpUtils
{
    internal static class AiRetryPolicy
    {
        public const int MaxAttempts = 3;
        public const int BaseBackoffMs = 600;

        public static bool IsTransientStatus(int status)
        {
            return status is 408 or 425 or 429 or >= 500 and <= 599;
        }

        public static bool IsTransientException(Exception ex)
        {
            return ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                or IOException;
        }

        public static void Sleep(int attempt, int baseBackoffMs = BaseBackoffMs)
        {
            Thread.Sleep(BackoffDelayMs(attempt, baseBackoffMs));
        }

        public static int BackoffDelayMs(int attempt, int baseBackoffMs = BaseBackoffMs)
        {
            return baseBackoffMs * (1 << (attempt - 1));
        }
    }
}
