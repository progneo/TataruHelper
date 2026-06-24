using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Translation.Http;
using Translation.Models;
using Translation.Settings;
using Translation.Utils;

namespace Translation.Providers.Papago
{
    class PapagoTranslator : ITranslationProvider
    {
        public TranslationEngineName EngineName => TranslationEngineName.Papago;

        const string TranslateUrl = "https://papago.naver.com/api/text/translation";

        const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
            "(KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

        readonly ILogger _Logger;
        readonly TranslationSettings _Settings;

        public PapagoTranslator(ILogger logger, TranslationSettings settings)
        {
            _Logger = logger;
            _Settings = settings ?? new TranslationSettings();
        }

        public Task<string> TranslateAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            return TranslationHttpPolicy.ExecuteTranslationWithRetryAsync(
                () => TranslateInternalAsync(sentence, inLang, outLang, cancellationToken),
                _Settings,
                _Logger,
                "Papago translate",
                cancellationToken);
        }

        async Task<string> TranslateInternalAsync(string sentence, string inLang, string outLang,
            CancellationToken cancellationToken)
        {
            if (string.IsNullOrEmpty(inLang))
                return string.Empty;

            var formFields = new Dictionary<string, string>
            {
                ["source"] = inLang,
                ["target"] = outLang,
                ["text"] = sentence,
                ["dict"] = "false",
                ["useGlossary"] = "false",
                ["honorific"] = "false"
            };

            try
            {
                using (var request = new HttpRequestMessage(HttpMethod.Post, TranslateUrl))
                {
                    request.Content = new FormUrlEncodedContent(formFields);

                    request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
                    request.Headers.Add("device-type", "pc");
                    request.Headers.Add("Origin", "https://papago.naver.com");
                    request.Headers.Add("Referer", "https://papago.naver.com/");
                    request.Headers.Add("Sec-Fetch-Site", "same-origin");
                    request.Headers.Add("Sec-Fetch-Mode", "cors");
                    request.Headers.Add("Sec-Fetch-Dest", "empty");

                    using (var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                               .ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync(cancellationToken)
                            .ConfigureAwait(false);

                        if (response.IsSuccessStatusCode)
                        {
                            var parsed = SafeJson.DeserializeExternal<PapagoResponse>(body);
                            return parsed?.translatedText ?? string.Empty;
                        }

                        _Logger?.LogInformation("{Message}",
                            "[PAPAGO_HTTP_" + (int)response.StatusCode + "] " + body);
                        return string.Empty;
                    }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception e)
            {
                _Logger?.LogInformation("{Message}", e.ToString());
                return string.Empty;
            }
        }
    }
}