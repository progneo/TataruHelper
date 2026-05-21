using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Jurassic;

using Newtonsoft.Json;

namespace Translation.Papago
{
    class PapagoEncoder
    {
        public bool IsAvaliable => _IsAvaliable;

        bool _IsAvaliable;

        readonly string ResourceFilePath;

        ScriptEngine PapgoEncoderEngine;

        readonly ILog _Logger;

        public PapagoEncoder(string resourceFilePath, ILog logger)
        {
            _Logger = logger;
            ResourceFilePath = resourceFilePath;
            Init();
        }

        private void Init()
        {
            try
            {
                string js = File.ReadAllText(ResourceFilePath);

                PapgoEncoderEngine = new ScriptEngine();
                PapgoEncoderEngine.Evaluate(js);
                _IsAvaliable = true;
            }
            catch (Exception e)
            {
                _Logger?.WriteLog(e.ToString());
                _IsAvaliable = false;
            }
        }

        public PapagoSerializedRequest EncodePapagoTranslationRequest(
            PapagoTranslationRequest translationRequest,
            string hmacKey)
        {
            if (!_IsAvaliable || PapgoEncoderEngine == null || string.IsNullOrEmpty(hmacKey))
                return null;

            try
            {
                string requestJson = JsonConvert.SerializeObject(translationRequest);
                string encoded = PapgoEncoderEngine.CallGlobalFunction<string>(
                    "EncodeTransaltionRequest", requestJson, hmacKey);

                var encodedRequest = JsonConvert.DeserializeObject<PapagoEncodedRequest>(encoded);

                string signature = PapagoHmacFin(encodedRequest.HmacInput, encodedRequest.HmacKey);
                string authorizationHeader = $"PPG {encodedRequest.Guid}:{signature}";

                return new PapagoSerializedRequest
                {
                    AuthorizationHeader = authorizationHeader,
                    StringRequest = encodedRequest.EncodedTranslationRequest,
                    Timestamp = encodedRequest.GuidTime
                };
            }
            catch (Exception e)
            {
                _Logger?.WriteLog(e.ToString());
                return null;
            }
        }

        static string PapagoHmacFin(string plaintext, string transactionKey)
        {
            var data = Encoding.UTF8.GetBytes(plaintext);
            var key = Encoding.UTF8.GetBytes(transactionKey);

            using (var hmac = new HMACMD5(key))
            {
                var hashBytes = hmac.ComputeHash(data);
                return Convert.ToBase64String(hashBytes);
            }
        }
    }
}