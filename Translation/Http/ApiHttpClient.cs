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
            var handler = new SocketsHttpHandler
            {
                // The handler both announces these and unpacks them, so nothing
                // may set Accept-Encoding by hand: announcing an encoding we
                // cannot unpack returns bytes nobody can read.
                AutomaticDecompression =
                    DecompressionMethods.GZip | DecompressionMethods.Deflate | DecompressionMethods.Brotli,

                // No engine here wants cookies, but the handler kept them by
                // default - one jar for the whole run. A service that answers a
                // refusal with a cookie then recognises us by it for as long as
                // the application is open, whatever else changes underneath.
                // Turning a translator off and on again cleared it; that is not
                // a thing anyone should have to work out.
                UseCookies = false,

                // Connections here live as long as the application does, and so
                // does the address each one resolved to. Bringing up a VPN, or
                // any other change of route, leaves the pool talking over the
                // old path until something forces a reconnect. Two minutes is
                // short enough that a change is picked up on its own and long
                // enough that nothing reconnects per line.
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
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