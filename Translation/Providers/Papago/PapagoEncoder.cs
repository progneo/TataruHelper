using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Jurassic;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace Translation.Providers.Papago
{
    class PapagoEncoder
    {
        public bool IsAvailable => _IsAvailable;

        bool _IsAvailable;

        readonly string ResourceFilePath;

        ScriptEngine PapagoEncoderEngine;

        readonly ILogger _Logger;

        public PapagoEncoder(string resourceFilePath, ILogger logger)
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

                PapagoEncoderEngine = new ScriptEngine();
                PapagoEncoderEngine.Evaluate(js);
                _IsAvailable = true;
            }
            catch (Exception e)
            {
                _Logger?.LogInformation("{Message}", e.ToString());
                _IsAvailable = false;
            }
        }

        public PapagoSerializedRequest EncodePapagoTranslationRequest(
            PapagoTranslationRequest translationRequest,
            string hmacKey)
        {
            if (!_IsAvailable || PapagoEncoderEngine == null || string.IsNullOrEmpty(hmacKey))
                return null;

            try
            {
                string requestJson = JsonConvert.SerializeObject(translationRequest);
                string encoded = PapagoEncoderEngine.CallGlobalFunction<string>(
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
                _Logger?.LogInformation("{Message}", e.ToString());
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