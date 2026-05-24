using System;
using System.Net;

using HttpUtilities;

using Translation.Http;
using Translation.Utils;

using ILog = Translation.Abstractions.ILog;

namespace Translation.Providers.Papago
{
    class PapagoTranslator
    {
        const string TranslateUrl = "https://papago.naver.com/apis/n2mt/translate";

        HttpReader _PapagoReader;
        PapagoEncoder _PapagoEncoder;
        readonly PapagoKeyResolver _KeyResolver;

        readonly ILog _Logger;

        public PapagoTranslator(ILog logger)
        {
            _Logger = logger;
            _KeyResolver = new PapagoKeyResolver(logger);
            CreatePapagoReader();
        }

        void CreatePapagoReader()
        {
            _PapagoReader = new HttpReader(new HttpILogWrapper(_Logger));
            TranslationHttpPolicy.ConfigureReader(_PapagoReader);
            _PapagoReader.ContentType = "application/x-www-form-urlencoded; charset=UTF-8";
        }

        public string Translate(string sentence, string inLang, string outLang)
        {
            return TranslationHttpPolicy.ExecuteTranslationWithRetry(
                () => TranslateInternal(sentence, inLang, outLang),
                _Logger,
                "Papago translate");
        }

        string TranslateInternal(string sentence, string inLang, string outLang)
        {
            if (string.IsNullOrEmpty(inLang))
                return string.Empty;

            sentence = sentence.Replace(":", " : ");

            if (_PapagoEncoder == null)
                _PapagoEncoder = new PapagoEncoder(GlobalTranslationSettings.PapagoEncoderPath, _Logger);

            if (!_PapagoEncoder.IsAvaliable)
                return string.Empty;

            var result = TryTranslateOnce(sentence, inLang, outLang, out bool authFailure);
            if (authFailure)
            {
                // HMAC key probably rotated — drop the cache and retry once.
                _KeyResolver.Invalidate();
                result = TryTranslateOnce(sentence, inLang, outLang, out _);
            }

            return result ?? string.Empty;
        }

        string TryTranslateOnce(string sentence, string inLang, string outLang, out bool authFailure)
        {
            authFailure = false;

            string hmacKey = _KeyResolver.GetKey();
            if (string.IsNullOrEmpty(hmacKey))
            {
                _Logger?.WriteLog("Papago translate skipped: HMAC key could not be resolved.");
                return string.Empty;
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
                _Logger?.WriteLog("Papago translate skipped: encoder returned null.");
                return string.Empty;
            }

            try
            {
                _PapagoReader.OptionalHeaders.Clear();

                _PapagoReader.OptionalHeaders.Add("device-type", "pc");
                _PapagoReader.OptionalHeaders.Add("x-apigw-partnerid", "papago");
                _PapagoReader.OptionalHeaders.Add("Origin", "https://papago.naver.com");
                _PapagoReader.OptionalHeaders.Add("Referer", "https://papago.naver.com/");
                _PapagoReader.OptionalHeaders.Add("Sec-Fetch-Site", "same-origin");
                _PapagoReader.OptionalHeaders.Add("Sec-Fetch-Mode", "cors");
                _PapagoReader.OptionalHeaders.Add("Sec-Fetch-Dest", "empty");

                _PapagoReader.OptionalHeaders.Add("Authorization", reqvObj.AuthorizationHeader);
                _PapagoReader.OptionalHeaders.Add("Timestamp", reqvObj.Timestamp);

                var requestBody = reqvObj.StringRequest +
                                  $"&authroization={Uri.EscapeDataString(reqvObj.AuthorizationHeader)}" +
                                  $"&timestamp={reqvObj.Timestamp}";

                var response = TranslationHttpPolicy.ExecuteHttpRequestWithRetry(
                    () => _PapagoReader.RequestWebData(TranslateUrl, HttpMethods.POST, requestBody, true),
                    _Logger,
                    "Papago translate");

                if (response.IsSuccessful)
                {
                    var parsed = SafeJson.DeserializeExternal<PapagoResponse>(response.Body);
                    return parsed?.translatedText ?? string.Empty;
                }

                authFailure = IsAuthFailure(response?.InnerException);

                CreatePapagoReader();
                _Logger?.WriteLog(response?.InnerException?.ToString() ?? "Papago Exception is null");
                return string.Empty;
            }
            catch (Exception e)
            {
                _Logger?.WriteLog(e.ToString());
                return string.Empty;
            }
        }

        static bool IsAuthFailure(Exception ex)
        {
            if (!(ex is WebException webEx) || !(webEx.Response is HttpWebResponse httpResp))
                return false;

            var status = (int)httpResp.StatusCode;
            return status == 401 || status == 403;
        }
    }
}