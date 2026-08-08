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
using Translation.Providers.AI;

namespace Translation.Providers.Gemini;

// Google Gemini.
// Docs: https://ai.google.dev/api/generate-content
// Endpoint: POST https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent
// Auth:     x-goog-api-key: <api-key>
internal sealed class GeminiTranslator : ITranslationProvider
{
    public TranslationEngineName EngineName => TranslationEngineName.Gemini;

    private const string EndpointBase = "https://generativelanguage.googleapis.com/v1beta/models/";
    private const string DefaultModel = "gemini-3.5-flash-lite";

    private readonly ILogger _logger;
    private readonly ITranslationCredentialStore _credentials;

    public GeminiTranslator(ILogger logger, ITranslationCredentialStore credentials)
    {
        _logger = logger;
        _credentials = credentials ?? NullCredentialStore.Instance;
    }

    public async Task<string> TranslateAsync(string sentence, string inLang, string outLang,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrEmpty(sentence))
            return string.Empty;

        var apiKey = _credentials.GetApiKey(TranslationEngineName.Gemini);
        if (string.IsNullOrWhiteSpace(apiKey))
            throw new MissingApiKeyException(TranslationEngineName.Gemini);

        var configuredModel = _credentials.GetModel(TranslationEngineName.Gemini);
        var model = string.IsNullOrWhiteSpace(configuredModel) ? DefaultModel : configuredModel;
        var endpoint = EndpointBase + model + ":generateContent";

        var systemPrompt = FfxivTranslationPrompt.BuildSystemPrompt(inLang, outLang);

        var payloadText = new JObject
        {
            ["systemInstruction"] = new JObject
            {
                ["parts"] = new JArray { new JObject { ["text"] = systemPrompt } },
            },
            ["contents"] = new JArray
            {
                new JObject
                {
                    ["role"] = "user",
                    ["parts"] = new JArray { new JObject { ["text"] = sentence } },
                },
            },
            ["generationConfig"] = new JObject { ["temperature"] = 0.2 },
        }.ToString(Formatting.None);

        Exception lastException = null;

        for (var attempt = 1; attempt <= AiRetryPolicy.MaxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, endpoint);
            request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("x-goog-api-key", apiKey);

            try
            {
                using var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                    .ConfigureAwait(false);
                var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                var status = (int)response.StatusCode;

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    throw new QuotaExceededException(TranslationEngineName.Gemini,
                        "Gemini quota exceeded (HTTP " + status + ").");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.LogInformation("{Message}",
                        "[Gemini_HTTP_" + status + "_ATTEMPT_" + attempt + "] " + body);
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

                _logger?.LogInformation("{Message}", "[Gemini_EMPTY_CONTENT_ATTEMPT_" + attempt + "] " + body);

                if (attempt < AiRetryPolicy.MaxAttempts)
                {
                    await AiRetryPolicy.DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return string.Empty;
            }
            catch (QuotaExceededException)
            {
                throw;
            }
            catch (MissingApiKeyException)
            {
                throw;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                lastException = ex;
                _logger?.LogInformation("{Message}", "[Gemini_EXCEPTION_ATTEMPT_" + attempt + "] " + ex);

                if (attempt < AiRetryPolicy.MaxAttempts && AiRetryPolicy.IsTransientException(ex))
                {
                    await AiRetryPolicy.DelayAsync(attempt, cancellationToken).ConfigureAwait(false);
                    continue;
                }

                return string.Empty;
            }
        }

        _logger?.LogInformation("{Message}", "[Gemini_EXHAUSTED_RETRIES] " + lastException);
        return string.Empty;
    }

    internal static string ParseContent(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return string.Empty;

        var text = JToken.Parse(body)?["candidates"]?[0]?["content"]?["parts"]?[0]?["text"]?.ToString();
        return AiResponseSanitizer.StripWrappingArtifacts(text);
    }
}