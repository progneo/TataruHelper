using System;
using System.Net;
using System.Net.Http;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Translation.Credentials;
using Translation.Exceptions;
using Translation.Http;

namespace Translation.Providers.GoogleCloud
{
    internal sealed class GoogleCloudTranslator
    {
        private const string Endpoint = "https://translation.googleapis.com/language/translate/v2";

        private readonly ILog _logger;
        private readonly ITranslationCredentialStore _credentials;

        public GoogleCloudTranslator(ILog logger, ITranslationCredentialStore credentials)
        {
            _logger = logger;
            _credentials = credentials ?? NullCredentialStore.Instance;
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            if (string.IsNullOrEmpty(sentence))
                return string.Empty;

            var apiKey = _credentials.GetApiKey(TranslationEngineName.GoogleCloudTranslate);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new MissingApiKeyException(TranslationEngineName.GoogleCloudTranslate);

            var target = string.IsNullOrWhiteSpace(outLang) ? "en" : outLang;
            var source = string.IsNullOrWhiteSpace(inLang) || inLang == "auto" ? null : inLang;

            var payload = new JObject { ["q"] = sentence, ["target"] = target, ["format"] = "text", };
            if (source != null)
                payload["source"] = source;

            using var request =
                new HttpRequestMessage(HttpMethod.Post, Endpoint + "?key=" + Uri.EscapeDataString(apiKey));
            request.Content =
                new StringContent(payload.ToString(Formatting.None), Encoding.UTF8, "application/json");

            try
            {
                using var response = ApiHttpClient.SendSync(request);
                var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                if (response.StatusCode == (HttpStatusCode)429 ||
                    (response.StatusCode == HttpStatusCode.Forbidden &&
                     (responseBody.Contains("quota", StringComparison.OrdinalIgnoreCase) ||
                      responseBody.Contains("userRateLimitExceeded", StringComparison.OrdinalIgnoreCase) ||
                      responseBody.Contains("dailyLimitExceeded", StringComparison.OrdinalIgnoreCase))))
                {
                    throw new QuotaExceededException(TranslationEngineName.GoogleCloudTranslate,
                        "Google Cloud Translation quota exceeded (HTTP " + (int)response.StatusCode + ").");
                }

                if (response.IsSuccessStatusCode)
                {
                    return ParseTranslation(responseBody);
                }

                _logger?.WriteLog("[GCLOUD_HTTP_" + (int)response.StatusCode + "] " + responseBody);
                return string.Empty;
            }
            catch (QuotaExceededException) { throw; }
            catch (MissingApiKeyException) { throw; }
            catch (Exception ex)
            {
                _logger?.WriteLog("[GCLOUD_EXCEPTION] " + ex);
                return string.Empty;
            }
        }

        internal static string ParseTranslation(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            var translations = JToken.Parse(body)?["data"]?["translations"] as JArray;
            if (translations == null || translations.Count == 0)
                return string.Empty;

            return translations[0]?["translatedText"]?.ToString() ?? string.Empty;
        }
    }
}