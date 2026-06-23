using System;
using System.IO;

using Microsoft.Extensions.Logging;

using Newtonsoft.Json;

namespace Translation.Utils
{
    static class JsonDataLoader
    {
        public static T LoadJsonData<T>(string path, ILogger logger = null)
        {
            T result = (T)Activator.CreateInstance(typeof(T));

            try
            {
                using (TextReader reader = new StreamReader(path))
                {
                    result = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                logger?.LogInformation("{Message}", Convert.ToString(e));

                try
                {
                    using (TextWriter writer = new StreamWriter(path))
                    {
                        writer.WriteLine(JsonConvert.SerializeObject(result, Formatting.Indented));
                    }
                }
                catch (Exception e1)
                {
                    logger?.LogInformation("{Message}", Convert.ToString(e1));
                }
            }

            return result;
        }
    }
}