using System;
using System.Net;
using System.Net.Http;
using System.Text;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

using Translation.Credentials;
using Translation.Exceptions;
using Translation.HttpUtils;

namespace Translation.AzureTranslator
{
    internal sealed class AzureTranslator
    {
        private const string Endpoint = "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0";

        private readonly ILog _logger;
        private readonly ITranslationCredentialStore _credentials;

        public AzureTranslator(ILog logger, ITranslationCredentialStore credentials)
        {
            _logger = logger;
            _credentials = credentials ?? NullCredentialStore.Instance;
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            if (string.IsNullOrEmpty(sentence))
                return string.Empty;

            var apiKey = _credentials.GetApiKey(TranslationEngineName.AzureTranslator);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new MissingApiKeyException(TranslationEngineName.AzureTranslator);

            var region = _credentials.GetRegion(TranslationEngineName.AzureTranslator);
            var source = string.IsNullOrWhiteSpace(inLang) || inLang == "auto" ? string.Empty : inLang;
            var target = string.IsNullOrWhiteSpace(outLang) ? "en" : outLang;

            var url = Endpoint + "&to=" + Uri.EscapeDataString(target);
            if (!string.IsNullOrEmpty(source))
                url += "&from=" + Uri.EscapeDataString(source);

            var body = "[{\"Text\":" + JsonConvert.SerializeObject(sentence) + "}]";

            using (var request = new HttpRequestMessage(HttpMethod.Post, url))
            {
                request.Content = new StringContent(body, Encoding.UTF8, "application/json");
                request.Headers.Add("Ocp-Apim-Subscription-Key", apiKey);
                if (!string.IsNullOrWhiteSpace(region))
                    request.Headers.Add("Ocp-Apim-Subscription-Region", region);

                try
                {
                    using (var response = ApiHttpClient.SendSync(request))
                    {
                        var responseBody = response.Content.ReadAsStringAsync().GetAwaiter().GetResult();

                        if (response.StatusCode == (HttpStatusCode)429 ||
                            (response.StatusCode == HttpStatusCode.Forbidden &&
                             responseBody.IndexOf("quota", StringComparison.OrdinalIgnoreCase) >= 0))
                        {
                            throw new QuotaExceededException(TranslationEngineName.AzureTranslator,
                                "Azure Translator quota exceeded (HTTP " + (int)response.StatusCode + ").");
                        }

                        if (!response.IsSuccessStatusCode)
                        {
                            _logger?.WriteLog("[AZURE_HTTP_" + (int)response.StatusCode + "] " + responseBody);
                            return string.Empty;
                        }

                        return ParseTranslation(responseBody);
                    }
                }
                catch (QuotaExceededException)
                {
                    throw;
                }
                catch (MissingApiKeyException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger?.WriteLog("[AZURE_EXCEPTION] " + ex);
                    return string.Empty;
                }
            }
        }

        internal static string ParseTranslation(string body)
        {
            if (string.IsNullOrWhiteSpace(body))
                return string.Empty;

            var token = JToken.Parse(body);
            var arr = token as JArray;
            if (arr == null || arr.Count == 0)
                return string.Empty;

            var translations = arr[0]["translations"] as JArray;
            if (translations == null || translations.Count == 0)
                return string.Empty;

            return translations[0]?["text"]?.ToString() ?? string.Empty;
        }
    }
}