using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json.Linq;

using Translation.Http;
using Translation.Models;
using Translation.Settings;

namespace Translation.Providers.Google
{
    class GoogleTranslator : ITranslationProvider
    {
        public TranslationEngineName EngineName => TranslationEngineName.GoogleTranslate;

        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";

        private const string BrowserAccept =
            "text/html,application/xhtml+xml,application/xml;q=0.9,application/json;q=0.8,*/*;q=0.7";

        private static readonly Regex GoogleRxLegacy =
            new Regex("(?<=(<div dir=\"ltr\" class=\"t0\">)).*?(?=(<\\/div>))",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private static readonly Regex GoogleRx =
            new Regex("(?<=(<div(.*)class=\"result-container\"(.*)>)).*?(?=(<\\/div>))",
                RegexOptions.Compiled | RegexOptions.IgnoreCase);

        private const string GoogleJsonBaseUrl =
            "https://translate.googleapis.com/translate_a/single?client=dict-chrome-ex&dt=t&dt=bd&dt=qca&dt=rm&dt=ss&dt=at&dt=ex&dt=ld&dt=md&dt=rw&ie=UTF-8&oe=UTF-8&otf=1&ssel=0&tsel=0&kc=7&hl={1}&sl={0}&tl={1}&q={2}";

        private const string GoogleHtmlBaseUrl =
            "https://translate.google.com/m?hl={0}&sl={1}&tl={2}&ie=UTF-8&prev=_m&q={3}";

        private readonly ILogger _logger;
        private readonly TranslationSettings _settings;

        public GoogleTranslator(ILogger logger, TranslationSettings settings)
        {
            _logger = logger;
            _settings = settings ?? new TranslationSettings();
        }


        public async Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            try
            {
                var safeInput = sentence ?? string.Empty;
                var sourceLang = string.IsNullOrWhiteSpace(inLang) ? "auto" : inLang;
                var targetLang = string.IsNullOrWhiteSpace(outLang) ? "en" : outLang;

                if (_settings.UseGoogleJsonEndpoint)
                {
                    var jsonResult = await TryTranslateUsingJsonEndpointAsync(safeInput, sourceLang, targetLang,
                        cancellationToken).ConfigureAwait(false);
                    if (IsValidGoogleResult(jsonResult))
                    {
                        return jsonResult;
                    }
                }

                if (_settings.UseGoogleHtmlFallbackEndpoint)
                {
                    var htmlResult = await TryTranslateUsingHtmlEndpointAsync(safeInput, sourceLang, targetLang,
                        cancellationToken).ConfigureAwait(false);
                    if (IsValidGoogleResult(htmlResult))
                    {
                        return htmlResult;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception exception)
            {
                _logger?.LogInformation("{Message}", $"[GOOGLE_TRANSLATE_EXCEPTION] {exception}");
            }

            return string.Empty;
        }

        private async Task<string> TryTranslateUsingJsonEndpointAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            var url = string.Format(
                GoogleJsonBaseUrl,
                inLang,
                outLang,
                Uri.EscapeDataString(sentence));

            var body = await TranslationHttpPolicy.ExecuteHttpRequestWithRetryAsync(
                () => GetStringAsync(url, "GOOGLE_JSON", cancellationToken),
                _settings,
                _logger,
                "Google translate json",
                cancellationToken).ConfigureAwait(false);

            if (body == null)
            {
                return string.Empty;
            }

            var parsed = ParseGoogleJsonTranslation(body, _logger);
            if (!IsValidGoogleResult(parsed))
            {
                _logger?.LogInformation("[GOOGLE_JSON_INVALID_RESULT] Parsed translation was empty or malformed.");
            }

            return parsed;
        }

        private async Task<string> TryTranslateUsingHtmlEndpointAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            var url = string.Format(
                GoogleHtmlBaseUrl,
                outLang,
                inLang,
                outLang,
                Uri.EscapeDataString(sentence));

            var body = await TranslationHttpPolicy.ExecuteHttpRequestWithRetryAsync(
                () => GetStringAsync(url, "GOOGLE_HTML", cancellationToken),
                _settings,
                _logger,
                "Google translate html",
                cancellationToken).ConfigureAwait(false);

            if (body == null)
            {
                return string.Empty;
            }

            var parsed = ParseGoogleHtmlTranslation(body);
            if (!IsValidGoogleResult(parsed))
            {
                _logger?.LogInformation("[GOOGLE_HTML_INVALID_RESULT] HTML parse failed.");
            }

            return parsed;
        }

        private async Task<string> GetStringAsync(string url, string logTag, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
                request.Headers.Accept.ParseAdd(BrowserAccept);
                request.Headers.AcceptLanguage.ParseAdd("en-US,en;q=0.8");

                using (var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                           .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                    {
                        _logger?.LogInformation("{Message}",
                            "[" + logTag + "_HTTP_" + (int)response.StatusCode + "] " + url);
                        return null;
                    }

                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        internal static string ParseGoogleJsonTranslation(string body, ILogger logger = null)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            try
            {
                var token = JToken.Parse(body);
                var rootArray = token as JArray;
                if (rootArray == null || rootArray.Count == 0)
                {
                    logger?.LogInformation("[GOOGLE_JSON_PARSE_ERROR] Root JSON token is not an array.");
                    return string.Empty;
                }

                var translatedFragments = rootArray[0] as JArray;
                if (translatedFragments == null)
                {
                    logger?.LogInformation("[GOOGLE_JSON_PARSE_ERROR] Segment array is missing.");
                    return string.Empty;
                }

                var builder = new StringBuilder();
                foreach (var fragmentToken in translatedFragments)
                {
                    var fragment = fragmentToken as JArray;
                    var translatedPart = fragment?[0]?.ToString();
                    if (string.IsNullOrEmpty(translatedPart))
                    {
                        continue;
                    }

                    builder.Append(translatedPart);
                }

                return WebUtility.HtmlDecode(builder.ToString().Trim());
            }
            catch (Exception exception)
            {
                logger?.LogInformation("{Message}", $"[GOOGLE_JSON_PARSE_EXCEPTION] {exception}");
                return string.Empty;
            }
        }

        internal static string ParseGoogleHtmlTranslation(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
            {
                return string.Empty;
            }

            var rxMatch = GoogleRxLegacy.Match(body);
            if (rxMatch.Success)
            {
                return WebUtility.HtmlDecode(rxMatch.Value.Trim());
            }

            rxMatch = GoogleRx.Match(body);
            if (rxMatch.Success)
            {
                return WebUtility.HtmlDecode(rxMatch.Value.Trim());
            }

            return string.Empty;
        }

        internal static bool IsValidGoogleResult(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var trimmed = value.Trim();
            if (trimmed.IndexOf("<div", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                return false;
            }

            return true;
        }
    }
}