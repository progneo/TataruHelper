using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Translation.Credentials;
using Translation.Exceptions;
using Translation.Http;
using Translation.Models;

namespace Translation.Providers.AI
{
    internal sealed class OpenAIChatClient
    {
        private readonly TranslationEngineName _engine;
        private readonly string _endpoint;
        private readonly string _defaultModel;
        private readonly ILogger _logger;
        private readonly ITranslationCredentialStore _credentials;

        public OpenAIChatClient(
            TranslationEngineName engine,
            string endpoint,
            string defaultModel,
            ILogger logger,
            ITranslationCredentialStore credentials)
        {
            _engine = engine;
            _endpoint = endpoint;
            _defaultModel = defaultModel;
            _logger = logger;
            _credentials = credentials ?? NullCredentialStore.Instance;
        }

        public async Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(sentence))
                return string.Empty;

            var apiKey = _credentials.GetApiKey(_engine);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new MissingApiKeyException(_engine);

            var configuredModel = _credentials.GetModel(_engine);
            var model = string.IsNullOrWhiteSpace(configuredModel) ? _defaultModel : configuredModel;

            var systemPrompt = FfxivTranslationPrompt.BuildSystemPrompt(inLang, outLang);

            var payloadText = new JObject
            {
                ["model"] = model,
                ["temperature"] = 0.2,
                ["messages"] = new JArray
                {
                    new JObject { ["role"] = "system", ["content"] = systemPrompt },
                    new JObject { ["role"] = "user", ["content"] = sentence },
                },
            }.ToString(Formatting.None);

            Exception lastException = null;

            for (var attempt = 1; attempt <= AiRetryPolicy.MaxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);

                try
                {
                    using var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                        .ConfigureAwait(false);
                    var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                    var status = (int)response.StatusCode;

                    if (response.StatusCode == (HttpStatusCode)429 ||
                        (!response.IsSuccessStatusCode &&
                         body.IndexOf("insufficient_quota", StringComparison.OrdinalIgnoreCase) >= 0))
                    {
                        throw new QuotaExceededException(_engine,
                            _engine + " quota exceeded (HTTP " + status + ").");
                    }

                    if (!response.IsSuccessStatusCode)
                    {
                        _logger?.LogInformation("{Message}",
                            "[" + _engine + "_HTTP_" + status + "_ATTEMPT_" + attempt + "] " + body);
                        if (AiRetryPolicy.IsTransientStatus(status) && attempt < AiRetryPolicy.MaxAttempts)
                        {
                            await AiRetryPolicy.DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                            continue;
                        }

                        return string.Empty;
                    }

                    var parsed = ParseContent(body);
                    if (!string.IsNullOrWhiteSpace(parsed))
                        return parsed;

                    _logger?.LogInformation("{Message}",
                        "[" + _engine + "_EMPTY_CONTENT_ATTEMPT_" + attempt + "] " + body);

                    if (attempt < AiRetryPolicy.MaxAttempts)
                    {
                        await AiRetryPolicy.DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return string.Empty;
                }
                catch (QuotaExceededException) { throw; }
                catch (MissingApiKeyException) { throw; }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger?.LogInformation("{Message}", "[" + _engine + "_EXCEPTION_ATTEMPT_" + attempt + "] " + ex);

                    if (attempt < AiRetryPolicy.MaxAttempts && AiRetryPolicy.IsTransientException(ex))
                    {
                        await AiRetryPolicy.DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                        continue;
                    }

                    return string.Empty;
                }
            }

            _logger?.LogInformation("{Message}", "[" + _engine + "_EXHAUSTED_RETRIES] " + lastException);
            return string.Empty;
        }

        internal static string ParseContent(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            var content = JToken.Parse(body)?["choices"]?[0]?["message"]?["content"]?.ToString();
            return AiResponseSanitizer.StripWrappingArtifacts(content);
        }
    }
}