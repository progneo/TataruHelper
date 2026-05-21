// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;

using HttpUtilities;

using Translation.HttpUtils;
using Translation.Utils;

namespace Translation.Papago
{
    class PapagoTranslator
    {
        HttpReader _PapagoReader;
        PapagoEncoder _PapagoEncoder = null;

        ILog _Logger;

        public PapagoTranslator(ILog logger)
        {
            _Logger = logger;

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
            sentence = sentence.Replace(":", " : ");
            string result = string.Empty;

            string url = @"https://papago.naver.com/apis/n2mt/translate";

            if (_PapagoEncoder == null)
                _PapagoEncoder = new PapagoEncoder(GlobalTranslationSettings.PapagoEncoderPath, _Logger);

            /*
            if (inLang == "auto")
                inLang = DetectLanguage(sentence);//*/
            if (inLang.Length == 0)
                return result;

            if (_PapagoEncoder.IsAvaliable)
            {
                try
                {
                    PapagoTranslationRequest papagoRequest = new PapagoTranslationRequest()
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
                        text = Uri.EscapeDataString(sentence)
                    };

                    var reqvObj = _PapagoEncoder.EncodePapagoTranslationRequest(papagoRequest);

                    if (reqvObj != null)
                    {
                        _PapagoReader.OptionalHeaders.Clear();

                        _PapagoReader.OptionalHeaders.Add("device-type", "pc");
                        _PapagoReader.OptionalHeaders.Add("x-apigw-partnerid", "papago");
                        _PapagoReader.OptionalHeaders.Add("Origin", @"https://papago.naver.com");
                        _PapagoReader.OptionalHeaders.Add("Sec-Fetch-Site", "same-origin");
                        _PapagoReader.OptionalHeaders.Add("Sec-Fetch-Mode", "cors");
                        _PapagoReader.OptionalHeaders.Add("Sec-Fetch-Dest", "empty");

                        _PapagoReader.OptionalHeaders.Add("Authorization", reqvObj.AuthorizationHeader);
                        _PapagoReader.OptionalHeaders.Add("Timestamp", reqvObj.Timestamp);

                        var requestBody = reqvObj.StringRequest +
                                          $"&authroization={Uri.EscapeDataString(reqvObj.AuthorizationHeader)}" +
                                          $"&timestamp={reqvObj.Timestamp}";

                        var papagoWebResponse = TranslationHttpPolicy.ExecuteHttpRequestWithRetry(
                            () => _PapagoReader.RequestWebData(url, HttpMethods.POST, requestBody, true),
                            _Logger,
                            "Papago translate");

                        if (papagoWebResponse.IsSuccessful)
                        {
                            PapagoResponse papagoResponse =
                                SafeJson.DeserializeExternal<PapagoResponse>(papagoWebResponse.Body);

                            result = papagoResponse.translatedText;
                        }
                        else
                        {
                            CreatePapagoReader();

                            _Logger?.WriteLog(papagoWebResponse?.InnerException?.ToString() ??
                                              "Papago Exception is null");
                        }
                    }
                    else
                    {
                        _Logger?.WriteLog("reqvObj == null");
                    }
                }
                catch (Exception e)
                {
                    _Logger?.WriteLog(e.ToString());
                }
            }

            if (result == null)
                result = string.Empty;

            return result;
        }

        string DetectLanguage(string sentence)
        {
            string result = string.Empty;

            if (_PapagoEncoder == null)
                _PapagoEncoder = new PapagoEncoder(GlobalTranslationSettings.PapagoEncoderPath, _Logger);

            if (_PapagoEncoder.IsAvaliable)
            {
                throw new NotImplementedException($"{nameof(DetectLanguage)} is not implementd");
                /*
                try
                {
                    PapagoDetectLanguageRequest papagoRequest = new PapagoDetectLanguageRequest()
                    {
                        query = sentence
                    };

                    var reqv = _PapagoEncoder.EncodePapagoTranslationRequest(JsonConvert.SerializeObject(papagoRequest));

                    var tmpResponse = _PapagoReader.RequestWebData(url, HttpUtilities.HttpMethods.POST, reqv);

                    PapagoDetectLanguageResponse papagoResponse = SafeJson.DeserializeExternal<PapagoDetectLanguageResponse>(tmpResponse.Body);

                    result = papagoResponse.langCode;

                }
                catch (Exception e)
                {
                    _Logger?.WriteLog(e.ToString());
                }//*/
            }

            return result;
        }
    }
}