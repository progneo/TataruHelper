using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Translation.Exceptions;
using Translation.Http;
using Translation.Models;
using Translation.Settings;
using Translation.Utils;

namespace Translation.Providers.Bing
{
    /// <summary>
    /// Microsoft Translator through the page anyone can open in a browser, with
    /// no key and no account - the same service as <see cref="Azure"/>'s engine
    /// reaches with one.
    ///
    /// The price of no key is that the credentials come off the page and expire,
    /// which makes this as fragile as the other keyless engines: it works until
    /// Microsoft changes the page, and then it stops until someone looks. It is
    /// ranked below the keyed path for that reason.
    ///
    /// As of 2026-08-10 it is stopped: the page still hands out the session
    /// values this reads, but the translate call answers 401 with
    /// {"ShowCaptcha":false} - abuse prevention refusing a caller it cannot see
    /// running a browser. Off by default until that changes.
    /// </summary>
    internal sealed class BingFreeTranslator : ITranslationProvider
    {
        private const string PageUrl = "https://www.bing.com/translator";
        private const string TranslateUrlFormat = "https://www.bing.com/ttranslatev3?isVertical=1&IG={0}&IID={1}";

        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        public TranslationEngineName EngineName => TranslationEngineName.BingFree;

        private readonly ILogger _logger;
        private readonly TranslationSettings _settings;

        private readonly SemaphoreSlim _credentialsGate = new SemaphoreSlim(1, 1);
        private BingSessionCredentials _credentials;

        public BingFreeTranslator(ILogger logger, TranslationSettings settings)
        {
            _logger = logger;
            _settings = settings ?? new TranslationSettings();
        }

        public Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            return TranslationHttpPolicy.ExecuteTranslationWithRetryAsync(
                () => TranslateInternalAsync(sentence, inLang, outLang, cancellationToken),
                _settings,
                _logger,
                "Bing translate",
                cancellationToken);
        }

        private async Task<string> TranslateInternalAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sentence) || string.IsNullOrEmpty(outLang))
                return string.Empty;

            try
            {
                var credentials = await GetCredentialsAsync(false, cancellationToken).ConfigureAwait(false);
                if (credentials == null)
                    return string.Empty;

                var translated = await PostAsync(credentials, sentence, inLang, outLang, cancellationToken)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(translated))
                    return translated;

                // The page can retire a token before it says it would. One forced
                // re-read tells a stale token apart from a real failure; more than
                // one would just re-fetch the page on every untranslatable line.
                credentials = await GetCredentialsAsync(true, cancellationToken).ConfigureAwait(false);
                if (credentials == null)
                    return string.Empty;

                return await PostAsync(credentials, sentence, inLang, outLang, cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (QuotaExceededException) { throw; }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
            catch (Exception e)
            {
                _logger?.LogInformation("{Message}", e.ToString());
                return string.Empty;
            }
        }

        private async Task<string> PostAsync(BingSessionCredentials credentials, string sentence,
            string inLang, string outLang, CancellationToken cancellationToken)
        {
            var endpoint = string.Format(
                TranslateUrlFormat,
                Uri.EscapeDataString(credentials.Ig),
                Uri.EscapeDataString(credentials.Iid));

            var fields = new Dictionary<string, string>
            {
                // The page spells "detect it yourself" differently from everyone else.
                ["fromLang"] = string.IsNullOrWhiteSpace(inLang) || inLang == "auto" ? "auto-detect" : inLang,
                ["to"] = outLang,
                ["text"] = sentence,
                ["token"] = credentials.Token,
                ["key"] = credentials.Key,
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new FormUrlEncodedContent(fields);
            request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
            request.Headers.Add("Origin", "https://www.bing.com");
            request.Headers.Add("Referer", PageUrl);

            using var response = await ApiHttpClient.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (response.StatusCode == (HttpStatusCode)429)
            {
                throw new QuotaExceededException(EngineName,
                    EngineName + " is rate limiting this address (HTTP 429).");
            }

            if (!response.IsSuccessStatusCode)
            {
                _logger?.LogInformation("{Message}",
                    "[BING_HTTP_" + (int)response.StatusCode + "] " + body);
                return string.Empty;
            }

            return ParseContent(body);
        }

        private async Task<BingSessionCredentials> GetCredentialsAsync(bool force,
            CancellationToken cancellationToken)
        {
            var cached = _credentials;
            if (!force && cached != null && cached.IsUsableAt(DateTime.UtcNow))
                return cached;

            await _credentialsGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                // Another line may have refreshed them while this one waited.
                cached = _credentials;
                if (!force && cached != null && cached.IsUsableAt(DateTime.UtcNow))
                    return cached;

                using var request = new HttpRequestMessage(HttpMethod.Get, PageUrl);
                request.Headers.UserAgent.ParseAdd(BrowserUserAgent);

                using var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("{Message}",
                        "[BING_PAGE_HTTP_" + (int)response.StatusCode + "]");
                    return null;
                }

                var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                var parsed = BingSessionCredentials.TryParse(html, DateTime.UtcNow);

                if (parsed == null)
                {
                    _logger?.LogInformation("{Message}",
                        "[BING_PAGE_UNRECOGNISED] the translator page did not carry the expected session values");
                }

                _credentials = parsed;
                return parsed;
            }
            finally
            {
                _credentialsGate.Release();
            }
        }

        /// <summary>
        /// A reply is an array with one entry per sentence sent, and we send one.
        /// Anything else - an error object, a consent page - reads as no
        /// translation rather than as an exception.
        /// </summary>
        internal static string ParseContent(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            try
            {
                var parsed = SafeJson.DeserializeExternal<JToken>(body);
                return (parsed as JArray)?[0]?["translations"]?[0]?["text"]?.ToString() ?? string.Empty;
            }
            catch (JsonException)
            {
                return string.Empty;
            }
            catch (ArgumentOutOfRangeException)
            {
                return string.Empty;
            }
        }
    }
}
