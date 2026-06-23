using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

using Newtonsoft.Json;

namespace Translation.Settings
{
    public static class TranslationSettingsStorage
    {
        public static TranslationSettings Load(string path, ILogger logger = null)
        {
            logger = logger ?? NullLogger.Instance;
            var settings = new TranslationSettings();

            try
            {
                if (!File.Exists(path))
                    return null;

                var pairs = JsonConvert.DeserializeObject<object[,]>(File.ReadAllText(path));
                if (pairs == null)
                    return null;

                var values = new Dictionary<string, object>(StringComparer.Ordinal);
                for (int i = 0; i < pairs.GetLength(0); i++)
                {
                    if (pairs[i, 0] is string name)
                        values[name] = pairs[i, 1];
                }

                foreach (var property in typeof(TranslationSettings).GetProperties(
                             BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!values.TryGetValue(property.Name, out var rawValue) || rawValue == null)
                        continue;

                    try
                    {
                        property.SetValue(settings, Convert.ChangeType(rawValue, property.PropertyType));
                    }
                    catch (Exception ex)
                    {
                        logger.LogInformation("{Message}",
                            $"Translation settings: value for '{property.Name}' is invalid, keeping default. {ex.Message}");
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                logger.LogInformation("{Message}", "Failed to load translation settings: " + ex);
                return null;
            }
        }

        public static bool Save(TranslationSettings settings, string path, ILogger logger = null)
        {
            logger = logger ?? NullLogger.Instance;

            try
            {
                var properties = typeof(TranslationSettings).GetProperties(
                    BindingFlags.Instance | BindingFlags.Public);

                var pairs = new object[properties.Length, 2];
                for (int i = 0; i < properties.Length; i++)
                {
                    pairs[i, 0] = properties[i].Name;
                    pairs[i, 1] = properties[i].GetValue(settings);
                }

                File.WriteAllText(path, JsonConvert.SerializeObject(pairs, Formatting.Indented) + Environment.NewLine);
                return true;
            }
            catch (Exception ex)
            {
                logger.LogInformation("{Message}", "Failed to save translation settings: " + ex);
                return false;
            }
        }
    }
}