using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

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

        public static Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken = default)
        {
            return Shared.SendAsync(request, HttpCompletionOption.ResponseContentRead, cancellationToken);
        }
    }
}