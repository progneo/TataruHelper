using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Translation.Http
{
    internal static class ApiHttpClient
    {
        private static readonly Lazy<HttpClient> _shared = new Lazy<HttpClient>(CreateClient);

        private static volatile int _requestTimeoutMs = 10000;
        private static volatile int _readWriteTimeoutMs = 30000;

        public static HttpClient Shared => _shared.Value;

        public static void Configure(int requestTimeoutMs, int readWriteTimeoutMs)
        {
            if (_shared.IsValueCreated)
                return;

            _requestTimeoutMs = requestTimeoutMs;
            _readWriteTimeoutMs = readWriteTimeoutMs;
        }

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(_requestTimeoutMs + _readWriteTimeoutMs),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TataruHelper/1.0");
            return client;
        }

        public static Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default)
        {
            return Shared.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
    }
}