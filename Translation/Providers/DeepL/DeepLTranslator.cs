using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Translation.Exceptions;
using Translation.Http;
using Translation.Models;
using Translation.Settings;

namespace Translation.Providers.DeepL
{
    internal sealed class DeepLTranslator : ITranslationProvider
    {
        public TranslationEngineName EngineName => TranslationEngineName.DeepL;

        private const string Endpoint = "https://www2.deepl.com/jsonrpc";

        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

        private static long _requestId = InitializeRequestId();

        private readonly ILogger _logger;
        private readonly TranslationSettings _settings;

        public DeepLTranslator(ILogger logger, TranslationSettings settings)
        {
            _logger = logger;
            _settings = settings ?? new TranslationSettings();
        }


        public async Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sentence))
                return string.Empty;

            var source = string.IsNullOrWhiteSpace(inLang) ? "auto" : inLang;
            var target = string.IsNullOrWhiteSpace(outLang) ? "EN" : outLang.ToUpperInvariant();

            var id = Interlocked.Increment(ref _requestId);
            var timestamp = AdjustTimestamp(sentence, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var requestBody = BuildRequestBody(sentence, source, target, id, timestamp);

            try
            {
                var responseBody = await TranslationHttpPolicy.ExecuteHttpRequestWithRetryAsync(
                    () => PostJsonRpcAsync(requestBody, cancellationToken),
                    _settings,
                    _logger,
                    "DeepL web translate",
                    cancellationToken).ConfigureAwait(false);

                if (responseBody == null)
                    return string.Empty;

                return ParseTranslation(responseBody);
            }
            catch (QuotaExceededException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", "[DEEPL_EXCEPTION] " + ex);
                return string.Empty;
            }
        }

        private async Task<string> PostJsonRpcAsync(string requestBody, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, Endpoint))
            {
                request.Content = new StringContent(requestBody, Encoding.UTF8, "application/json");
                request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
                request.Headers.Accept.ParseAdd("*/*");
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.8");
                request.Headers.TryAddWithoutValidation("Origin", "https://www.deepl.com");
                request.Headers.TryAddWithoutValidation("Referer", "https://www.deepl.com/");

                using (var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                           .ConfigureAwait(false))
                {
                    var responseBody = await response.Content.ReadAsStringAsync(cancellationToken)
                        .ConfigureAwait(false);

                    if (response.StatusCode == (HttpStatusCode)429)
                    {
                        throw new QuotaExceededException(TranslationEngineName.DeepL,
                            "DeepL web endpoint rate-limited the request (HTTP 429). It clears on its own; " +
                            "wait a bit or switch to another engine.");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger?.LogInformation("{Message}",
                            "[DEEPL_HTTP_" + (int)response.StatusCode + "] " + responseBody);
                        return null;
                    }

                    return responseBody;
                }
            }
        }

        private static long InitializeRequestId()
        {
            return (long)Random.Shared.Next(8_300_000, 8_399_999) * 1000;
        }

        internal static long AdjustTimestamp(string text, long nowMilliseconds)
        {
            long iCount = 0;
            foreach (var c in text ?? string.Empty)
            {
                if (c == 'i')
                    iCount++;
            }

            if (iCount == 0)
                return nowMilliseconds;

            iCount++;
            return nowMilliseconds - nowMilliseconds % iCount + iCount;
        }

        internal static string BuildRequestBody(string text, string sourceLang, string targetLang, long id,
            long timestamp)
        {
            var payload = new JObject
            {
                ["jsonrpc"] = "2.0",
                ["method"] = "LMT_handle_texts",
                ["id"] = id,
                ["params"] = new JObject
                {
                    ["texts"] = new JArray
                    {
                        new JObject { ["text"] = text ?? string.Empty, ["requestAlternatives"] = 0 },
                    },
                    ["splitting"] = "newlines",
                    ["lang"] = new JObject
                    {
                        ["source_lang_user_selected"] = sourceLang,
                        ["target_lang"] = targetLang,
                    },
                    ["timestamp"] = timestamp,
                    ["commonJobParams"] = new JObject
                    {
                        ["wasSpoken"] = false,
                        ["transcribe_as"] = string.Empty,
                    },
                },
            };

            var body = payload.ToString(Formatting.None);

            var spacedMethod = (id + 5) % 29 == 0 || id % 13 == 0
                ? "\"method\" : \""
                : "\"method\": \"";

            return body.Replace("\"method\":\"", spacedMethod);
        }

        internal static string ParseTranslation(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            try
            {
                var texts = JToken.Parse(body)?["result"]?["texts"] as JArray;
                if (texts == null || texts.Count == 0)
                    return string.Empty;

                var sb = new StringBuilder();
                foreach (var t in texts)
                {
                    var text = t?["text"]?.ToString();
                    if (!string.IsNullOrEmpty(text))
                        sb.Append(text);
                }

                return sb.ToString();
            }
            catch (JsonException)
            {
                return string.Empty;
            }
        }
    }
}