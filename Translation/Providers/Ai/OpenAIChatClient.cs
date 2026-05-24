using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Translation.Credentials;
using Translation.Exceptions;
using Translation.Http;

namespace Translation.Providers.Ai
{
    internal sealed class OpenAIChatClient
    {
        private const int MaxAttempts = 3;
        private const int BaseBackoffMs = 600;

        private readonly TranslationEngineName _engine;
        private readonly string _endpoint;
        private readonly string _defaultModel;
        private readonly ILog _logger;
        private readonly ITranslationCredentialStore _credentials;

        public OpenAIChatClient(
            TranslationEngineName engine,
            string endpoint,
            string defaultModel,
            ILog logger,
            ITranslationCredentialStore credentials)
        {
            _engine = engine;
            _endpoint = endpoint;
            _defaultModel = defaultModel;
            _logger = logger;
            _credentials = credentials ?? NullCredentialStore.Instance;
        }

        public string Translate(string sentence, string inLang, string outLang)
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

            for (var attempt = 1; attempt <= MaxAttempts; attempt++)
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint);
                request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");
                request.Headers.TryAddWithoutValidation("Authorization", "Bearer " + apiKey);

                try
                {
                    using var response = ApiHttpClient.SendSync(request);
                    var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
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
                        _logger?.WriteLog("[" + _engine + "_HTTP_" + status + "_ATTEMPT_" + attempt + "] " +
                                          body);
                        if (IsTransientStatus(status) && attempt < MaxAttempts)
                        {
                            Sleep(attempt);
                            continue;
                        }

                        return string.Empty;
                    }

                    var parsed = ParseContent(body);
                    if (!string.IsNullOrWhiteSpace(parsed))
                        return parsed;

                    _logger?.WriteLog("[" + _engine + "_EMPTY_CONTENT_ATTEMPT_" + attempt + "] " + body);

                    if (attempt < MaxAttempts)
                    {
                        Sleep(attempt);
                        continue;
                    }

                    return string.Empty;
                }
                catch (QuotaExceededException) { throw; }
                catch (MissingApiKeyException) { throw; }
                catch (Exception ex)
                {
                    lastException = ex;
                    _logger?.WriteLog("[" + _engine + "_EXCEPTION_ATTEMPT_" + attempt + "] " + ex);

                    if (attempt < MaxAttempts && IsTransientException(ex))
                    {
                        Sleep(attempt);
                        continue;
                    }

                    return string.Empty;
                }
            }

            _logger?.WriteLog("[" + _engine + "_EXHAUSTED_RETRIES] " + lastException);
            return string.Empty;
        }

        private static bool IsTransientStatus(int status)
        {
            return status is 408 or 425 or 429 or >= 500 and <= 599;
        }

        private static bool IsTransientException(Exception ex)
        {
            return ex is HttpRequestException or TaskCanceledException or OperationCanceledException
                or IOException;
        }

        private static void Sleep(int attempt)
        {
            var delay = BaseBackoffMs * (1 << (attempt - 1));
            Thread.Sleep(delay);
        }

        internal static string ParseContent(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            var content = JToken.Parse(body)?["choices"]?[0]?["message"]?["content"]?.ToString();
            return StripWrappingArtifacts(content);
        }

        internal static string StripWrappingArtifacts(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return string.Empty;

            var trimmed = text.Trim();

            if (trimmed.StartsWith("```", StringComparison.Ordinal))
            {
                var firstNewline = trimmed.IndexOf('\n');
                if (firstNewline > 0)
                {
                    trimmed = trimmed[(firstNewline + 1)..];
                }

                if (trimmed.EndsWith("```", StringComparison.Ordinal))
                {
                    trimmed = trimmed[..^3];
                }

                trimmed = trimmed.Trim();
            }

            if (trimmed.Length >= 2 &&
                ((trimmed[0] == '"' && trimmed[^1] == '"') ||
                 (trimmed[0] == '\'' && trimmed[^1] == '\'') ||
                 (trimmed[0] == '“' && trimmed[^1] == '”')))
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            return trimmed;
        }
    }
}