// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

using HttpUtilities;

using Newtonsoft.Json.Linq;

using Translation.Http;

using ILog = Translation.Abstractions.ILog;

namespace Translation.Providers.Google
{
    class GoogleTranslator
    {
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

        private readonly ILog _logger;

        private HttpReader _googleWebReader;

        public GoogleTranslator(ILog logger)
        {
            _googleWebReader = null;
            _logger = logger;
        }

        private void CreateGoogleReader()
        {
            _googleWebReader = new HttpReader(new HttpILogWrapper(_logger));
            TranslationHttpPolicy.ConfigureReader(_googleWebReader);

            _googleWebReader.UserAgent =
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0 Safari/537.36";
            _googleWebReader.Accept =
                "text/html,application/xhtml+xml,application/xml;q=0.9,application/json;q=0.8,*/*;q=0.7";
            _googleWebReader.ContentType = null;

            _googleWebReader.OptionalHeaders.Add("Accept-Language", "en-US,en;q=0.8");
            _googleWebReader.OptionalHeaders.Add("DNT", "1");
            _googleWebReader.OptionalHeaders.Add("Upgrade-Insecure-Requests", "1");
            _googleWebReader.OptionalHeaders.Add("Pragma", "no-cache");
            _googleWebReader.OptionalHeaders.Add("Cache-Control", "no-cache");

            TranslationHttpPolicy.ExecuteHttpRequestWithRetry(
                () => _googleWebReader.RequestWebData("https://translate.google.com/m", HttpMethods.GET, true),
                _logger,
                "Google warmup");
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            if (_googleWebReader == null)
            {
                CreateGoogleReader();
            }

            var result = TranslateInternal(sentence, inLang, outLang);
            if (!IsValidGoogleResult(result))
            {
                CreateGoogleReader();
                result = TranslateInternal(sentence, inLang, outLang);
            }

            return result ?? string.Empty;
        }

        private string TranslateInternal(string sentence, string inLang, string outLang)
        {
            try
            {
                var safeInput = sentence ?? string.Empty;
                var sourceLang = string.IsNullOrWhiteSpace(inLang) ? "auto" : inLang;
                var targetLang = string.IsNullOrWhiteSpace(outLang) ? "en" : outLang;

                if (GlobalTranslationSettings.UseGoogleJsonEndpoint)
                {
                    var jsonResult = TryTranslateUsingJsonEndpoint(safeInput, sourceLang, targetLang);
                    if (IsValidGoogleResult(jsonResult))
                    {
                        return jsonResult;
                    }
                }

                if (GlobalTranslationSettings.UseGoogleHtmlFallbackEndpoint)
                {
                    var htmlResult = TryTranslateUsingHtmlEndpoint(safeInput, sourceLang, targetLang);
                    if (IsValidGoogleResult(htmlResult))
                    {
                        return htmlResult;
                    }
                }
            }
            catch (Exception exception)
            {
                _logger?.WriteLog($"[GOOGLE_TRANSLATE_EXCEPTION] {exception}");
            }

            return string.Empty;
        }

        private string TryTranslateUsingJsonEndpoint(string sentence, string inLang, string outLang)
        {
            var url = string.Format(
                GoogleJsonBaseUrl,
                inLang,
                outLang,
                Uri.EscapeDataString(sentence));

            var requestResult = TranslationHttpPolicy.ExecuteHttpRequestWithRetry(
                () => _googleWebReader.RequestWebData(url, HttpMethods.GET, true),
                _logger,
                "Google translate json");

            if (!requestResult.IsSuccessful)
            {
                _logger?.WriteLog($"[GOOGLE_JSON_HTTP_FAILED] {requestResult?.InnerException}");
                return string.Empty;
            }

            var parsed = ParseGoogleJsonTranslation(requestResult.Body, _logger);
            if (!IsValidGoogleResult(parsed))
            {
                _logger?.WriteLog("[GOOGLE_JSON_INVALID_RESULT] Parsed translation was empty or malformed.");
            }

            return parsed;
        }

        private string TryTranslateUsingHtmlEndpoint(string sentence, string inLang, string outLang)
        {
            var url = string.Format(
                GoogleHtmlBaseUrl,
                outLang,
                inLang,
                outLang,
                Uri.EscapeDataString(sentence));

            var requestResult = TranslationHttpPolicy.ExecuteHttpRequestWithRetry(
                () => _googleWebReader.RequestWebData(url, HttpMethods.GET, true),
                _logger,
                "Google translate html");

            if (!requestResult.IsSuccessful)
            {
                _logger?.WriteLog($"[GOOGLE_HTML_HTTP_FAILED] {requestResult?.InnerException}");
                return string.Empty;
            }

            var parsed = ParseGoogleHtmlTranslation(requestResult.Body);
            if (!IsValidGoogleResult(parsed))
            {
                _logger?.WriteLog("[GOOGLE_HTML_INVALID_RESULT] HTML parse failed.");
                _logger?.WriteLog(requestResult.Body ?? "[GOOGLE_HTML_EMPTY_BODY]");
            }

            return parsed;
        }

        internal static string ParseGoogleJsonTranslation(string body, ILog logger = null)
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
                    logger?.WriteLog("[GOOGLE_JSON_PARSE_ERROR] Root JSON token is not an array.");
                    return string.Empty;
                }

                var translatedFragments = rootArray[0] as JArray;
                if (translatedFragments == null)
                {
                    logger?.WriteLog("[GOOGLE_JSON_PARSE_ERROR] Segment array is missing.");
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
                logger?.WriteLog($"[GOOGLE_JSON_PARSE_EXCEPTION] {exception}");
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