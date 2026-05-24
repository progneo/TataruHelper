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

namespace Translation.Providers.Ai;

// YandexGPT (Yandex Foundation Models REST API).
// Docs: https://yandex.cloud/en/docs/foundation-models/text-generation/api-ref/TextGeneration/completion
// Endpoint: POST https://llm.api.cloud.yandex.net/foundationModels/v1/completion
// Auth:     Authorization: Api-Key <service-account-api-key>
//           x-folder-id:   <cloud-folder-id>
// Reuses the Yandex Cloud API key + folder id that are already configured for the
// Yandex Cloud Translate engine — both APIs accept the same credentials.
internal sealed class YandexGptTranslator
{
    private const string Endpoint = "https://llm.api.cloud.yandex.net/foundationModels/v1/completion";
    private const string DefaultModelAlias = "yandexgpt-lite/latest";
    private const int MaxAttempts = 3;
    private const int BaseBackoffMs = 600;

    private readonly ILog _logger;
    private readonly ITranslationCredentialStore _credentials;

    public YandexGptTranslator(ILog logger, ITranslationCredentialStore credentials)
    {
        _logger = logger;
        _credentials = credentials ?? NullCredentialStore.Instance;
    }

    public string Translate(string sentence, string inLang, string outLang)
    {
        if (string.IsNullOrEmpty(sentence))
            return string.Empty;

        var apiKey = _credentials.GetApiKey(TranslationEngineName.Yandex);
        var folderId = _credentials.GetRegion(TranslationEngineName.Yandex);

        if (string.IsNullOrWhiteSpace(apiKey) || string.IsNullOrWhiteSpace(folderId))
            throw new MissingApiKeyException(TranslationEngineName.YandexGPT);

        var configuredModel = _credentials.GetModel(TranslationEngineName.YandexGPT);
        var modelAlias = string.IsNullOrWhiteSpace(configuredModel) ? DefaultModelAlias : configuredModel;
        var modelUri = "gpt://" + folderId + "/" + modelAlias;

        var systemPrompt = FfxivTranslationPrompt.BuildSystemPrompt(inLang, outLang);

        var payloadText = new JObject
        {
            ["modelUri"] = modelUri,
            ["completionOptions"] =
                new JObject { ["stream"] = false, ["temperature"] = 0.2, ["maxTokens"] = "2000", },
            ["messages"] = new JArray
            {
                new JObject { ["role"] = "system", ["text"] = systemPrompt },
                new JObject { ["role"] = "user", ["text"] = sentence },
            },
        }.ToString(Formatting.None);

        Exception lastException = null;

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            request.Content = new StringContent(payloadText, Encoding.UTF8, "application/json");
            request.Headers.TryAddWithoutValidation("Authorization", "Api-Key " + apiKey);
            request.Headers.TryAddWithoutValidation("x-folder-id", folderId);

            try
            {
                using var response = ApiHttpClient.SendSync(request);
                var body = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                var status = (int)response.StatusCode;

                if (response.StatusCode == (HttpStatusCode)429)
                {
                    throw new QuotaExceededException(TranslationEngineName.YandexGPT,
                        "YandexGPT quota exceeded (HTTP " + status + ").");
                }

                if (!response.IsSuccessStatusCode)
                {
                    _logger?.WriteLog("[YandexGPT_HTTP_" + status + "_ATTEMPT_" + attempt + "] " + body);
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

                _logger?.WriteLog("[YandexGPT_EMPTY_CONTENT_ATTEMPT_" + attempt + "] " + body);

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
                _logger?.WriteLog("[YandexGPT_EXCEPTION_ATTEMPT_" + attempt + "] " + ex);

                if (attempt < MaxAttempts && IsTransientException(ex))
                {
                    Sleep(attempt);
                    continue;
                }

                return string.Empty;
            }
        }

        _logger?.WriteLog("[YandexGPT_EXHAUSTED_RETRIES] " + lastException);
        return string.Empty;
    }

    private static bool IsTransientStatus(int status)
    {
        return status == 408 || status == 425 || status == 429 ||
               (status >= 500 && status <= 599);
    }

    private static bool IsTransientException(Exception ex)
    {
        return ex is HttpRequestException or TaskCanceledException or OperationCanceledException or IOException;
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

        var text = JToken.Parse(body)?["result"]?["alternatives"]?[0]?["message"]?["text"]?.ToString();
        return OpenAIChatClient.StripWrappingArtifacts(text);
    }
}