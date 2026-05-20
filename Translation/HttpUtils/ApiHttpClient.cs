using System;
using System.Net;
using System.Net.Http;
using System.Threading;

namespace Translation.HttpUtils
{
    internal static class ApiHttpClient
    {
        private static readonly Lazy<HttpClient> _shared = new Lazy<HttpClient>(CreateClient);

        public static HttpClient Shared => _shared.Value;

        private static HttpClient CreateClient()
        {
            var handler = new HttpClientHandler
            {
                AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
            };

            var client = new HttpClient(handler)
            {
                Timeout = TimeSpan.FromMilliseconds(GlobalTranslationSettings.HttpRequestTimeoutMilliseconds +
                                                    GlobalTranslationSettings.HttpReadWriteTimeoutMilliseconds),
            };
            client.DefaultRequestHeaders.UserAgent.ParseAdd("TataruHelper/1.0");
            return client;
        }

        public static HttpResponseMessage SendSync(HttpRequestMessage request)
        {
            return Shared.SendAsync(request, HttpCompletionOption.ResponseContentRead, CancellationToken.None)
                .GetAwaiter().GetResult();
        }
    }
}