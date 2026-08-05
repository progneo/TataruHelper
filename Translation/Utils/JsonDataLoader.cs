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
            var fullPath = ResolvePath(path);

            try
            {
                using (TextReader reader = new StreamReader(fullPath))
                {
                    result = JsonConvert.DeserializeObject<T>(reader.ReadToEnd());
                }
            }
            catch (Exception e)
            {
                logger?.LogInformation("{Message}", Convert.ToString(e));

                try
                {
                    using (TextWriter writer = new StreamWriter(fullPath))
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

        /// <summary>
        /// The resource paths are relative and ship next to the executable, so
        /// they have to be resolved against it rather than against whatever the
        /// working directory happens to be. Launched from a shortcut with a
        /// different "start in", from a script, or elevated - where Windows
        /// hands the process System32 - the files are simply not found, and the
        /// caller substitutes an empty list. Every language picker then comes up
        /// blank with nothing in the interface to explain why.
        /// </summary>
        private static string ResolvePath(string path)
        {
            if (string.IsNullOrEmpty(path) || Path.IsPathRooted(path))
                return path;

            return Path.Combine(AppContext.BaseDirectory, path);
        }
    }
}
