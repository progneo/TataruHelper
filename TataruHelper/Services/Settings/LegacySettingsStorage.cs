using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;

using Newtonsoft.Json;

namespace FFXIVTataruHelper.Services.Settings
{
    // Reads and writes settings objects in the legacy on-disk format: a JSON
    // array of [name, value] pairs that the old reflection-based static-field
    // persistence produced. Loading is tolerant — values are matched by name,
    // unknown names are ignored, and missing names keep their defaults.
    internal static class LegacySettingsStorage
    {
        public static T Load<T>(string path) where T : class, new()
        {
            var settings = new T();

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

                foreach (var property in typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!values.TryGetValue(property.Name, out var rawValue) || rawValue == null)
                        continue;

                    try
                    {
                        property.SetValue(settings, Convert.ChangeType(rawValue, property.PropertyType));
                    }
                    catch (Exception ex)
                    {
                        Logger.WriteLog(
                            $"Settings: value for '{property.Name}' is invalid, keeping default. {ex.Message}");
                    }
                }

                return settings;
            }
            catch (Exception ex)
            {
                Logger.WriteLog("Failed to load settings from " + path);
                Logger.WriteLog(ex);
                return null;
            }
        }

        public static bool Save<T>(T settings, string path) where T : class
        {
            try
            {
                var properties = typeof(T).GetProperties(BindingFlags.Instance | BindingFlags.Public);

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
                Logger.WriteLog("Failed to save settings to " + path);
                Logger.WriteLog(ex);
                return false;
            }
        }
    }
}
