using System;
using System.Net.Http;
using System.Text;
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

        const string TranslateUrl = "https://papago.naver.com/apis/n2mt/translate";

        volatile PapagoEncoder _PapagoEncoder;
        readonly object _encoderSync = new object();
        readonly PapagoKeyResolver _KeyResolver;

        readonly ILogger _Logger;
        readonly TranslationSettings _Settings;

        public PapagoTranslator(ILogger logger, TranslationSettings settings)
        {
            _Logger = logger;
            _Settings = settings ?? new TranslationSettings();
            _KeyResolver = new PapagoKeyResolver(logger, _Settings.PapagoKeyCachePath);
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

            sentence = sentence.Replace(":", " : ");

            if (_PapagoEncoder == null)
            {
                lock (_encoderSync)
                {
                    if (_PapagoEncoder == null)
                        _PapagoEncoder = new PapagoEncoder(_Settings.PapagoEncoderPath, _Logger);
                }
            }

            if (!_PapagoEncoder.IsAvailable)
                return string.Empty;

            var (result, authFailure) =
                await TryTranslateOnceAsync(sentence, inLang, outLang, cancellationToken).ConfigureAwait(false);
            if (authFailure)
            {
                // HMAC key probably rotated — drop the cache and retry once.
                _KeyResolver.Invalidate();
                (result, _) =
                    await TryTranslateOnceAsync(sentence, inLang, outLang, cancellationToken).ConfigureAwait(false);
            }

            return result ?? string.Empty;
        }

        async Task<(string Result, bool AuthFailure)> TryTranslateOnceAsync(string sentence, string inLang,
            string outLang, CancellationToken cancellationToken)
        {
            string hmacKey = await _KeyResolver.GetKeyAsync(cancellationToken).ConfigureAwait(false);
            if (string.IsNullOrEmpty(hmacKey))
            {
                _Logger?.LogInformation("Papago translate skipped: HMAC key could not be resolved.");
                return (string.Empty, false);
            }

            var papagoRequest = new PapagoTranslationRequest
            {
                deviceId = "",
                dict = false,
                dictDisplay = 0,
                honorific = false,
                instant = false,
                paging = false,
                source = inLang,
                target = outLang,
                locale = "ko-KR",
                text = sentence
            };

            var reqvObj = _PapagoEncoder.EncodePapagoTranslationRequest(papagoRequest, hmacKey);
            if (reqvObj == null)
            {
                _Logger?.LogInformation("Papago translate skipped: encoder returned null.");
                return (string.Empty, false);
            }

            try
            {
                var requestBody = reqvObj.StringRequest +
                                  $"&authroization={Uri.EscapeDataString(reqvObj.AuthorizationHeader)}" +
                                  $"&timestamp={reqvObj.Timestamp}";

                using (var request = new HttpRequestMessage(HttpMethod.Post, TranslateUrl))
                {
                    request.Content = new StringContent(requestBody, Encoding.UTF8,
                        "application/x-www-form-urlencoded");

                    request.Headers.Add("device-type", "pc");
                    request.Headers.Add("x-apigw-partnerid", "papago");
                    request.Headers.Add("Origin", "https://papago.naver.com");
                    request.Headers.Add("Referer", "https://papago.naver.com/");
                    request.Headers.Add("Sec-Fetch-Site", "same-origin");
                    request.Headers.Add("Sec-Fetch-Mode", "cors");
                    request.Headers.Add("Sec-Fetch-Dest", "empty");
                    request.Headers.TryAddWithoutValidation("Authorization", reqvObj.AuthorizationHeader);
                    request.Headers.Add("Timestamp", reqvObj.Timestamp);

                    using (var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                               .ConfigureAwait(false))
                    {
                        var body = await response.Content.ReadAsStringAsync(cancellationToken)
                            .ConfigureAwait(false);

                        if (response.IsSuccessStatusCode)
                        {
                            var parsed = SafeJson.DeserializeExternal<PapagoResponse>(body);
                            return (parsed?.translatedText ?? string.Empty, false);
                        }

                        var status = (int)response.StatusCode;
                        var authFailure = status == 401 || status == 403;

                        _Logger?.LogInformation("{Message}", "[PAPAGO_HTTP_" + status + "] " + body);
                        return (string.Empty, authFailure);
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
                return (string.Empty, false);
            }
        }
    }
}