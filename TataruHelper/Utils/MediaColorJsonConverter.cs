using System;
using System.Globalization;
using System.Windows.Media;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FFXIVTataruHelper.Utils
{
    /// <summary>
    /// Serializes <see cref="Color"/> as an <c>#AARRGGBB</c> hex string and accepts
    /// either that form, the JSON object form Newtonsoft used to emit
    /// (<c>{ "A": 255, "R": 0, ... }</c> — which round-trips incorrectly because the
    /// ScA/ScR/ScG/ScB setters clobber A/R/G/B), or the legacy "named color" form.
    /// </summary>
    public sealed class MediaColorJsonConverter : JsonConverter
    {
        public override bool CanConvert(Type objectType) =>
            objectType == typeof(Color) || objectType == typeof(Color?);

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            if (value is null)
            {
                writer.WriteNull();
                return;
            }

            var c = (Color)value;
            writer.WriteValue($"#{c.A:X2}{c.R:X2}{c.G:X2}{c.B:X2}");
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue,
            JsonSerializer serializer)
        {
            if (reader.TokenType == JsonToken.Null)
            {
                return null;
            }

            if (reader.TokenType == JsonToken.String)
            {
                var text = (string)reader.Value;
                return ParseHexOrName(text);
            }

            if (reader.TokenType == JsonToken.StartObject)
            {
                var obj = JObject.Load(reader);

                // Prefer A/R/G/B; fall back to Sc* only if those are missing.
                byte a = ReadByte(obj, "A", 255);
                byte r = ReadByte(obj, "R", 0);
                byte g = ReadByte(obj, "G", 0);
                byte b = ReadByte(obj, "B", 0);

                if (!obj.ContainsKey("A") && obj.TryGetValue("ScA", out var scA))
                    a = (byte)Math.Clamp((int)Math.Round(scA.Value<double>() * 255.0), 0, 255);
                if (!obj.ContainsKey("R") && obj.TryGetValue("ScR", out var scR))
                    r = (byte)Math.Clamp((int)Math.Round(scR.Value<double>() * 255.0), 0, 255);
                if (!obj.ContainsKey("G") && obj.TryGetValue("ScG", out var scG))
                    g = (byte)Math.Clamp((int)Math.Round(scG.Value<double>() * 255.0), 0, 255);
                if (!obj.ContainsKey("B") && obj.TryGetValue("ScB", out var scB))
                    b = (byte)Math.Clamp((int)Math.Round(scB.Value<double>() * 255.0), 0, 255);

                return Color.FromArgb(a, r, g, b);
            }

            return Color.FromArgb(255, 255, 255, 255);
        }

        private static byte ReadByte(JObject obj, string name, byte fallback) =>
            obj.TryGetValue(name, out var token) && token.Type != JTokenType.Null
                ? (byte)Math.Clamp(token.Value<int>(), 0, 255)
                : fallback;

        private static Color ParseHexOrName(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return Color.FromArgb(255, 255, 255, 255);

            var s = text.Trim();
            if (s.StartsWith("#")) s = s[1..];

            try
            {
                if (s.Length == 6)
                {
                    var r = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    var g = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    var b = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Color.FromArgb(255, r, g, b);
                }

                if (s.Length == 8)
                {
                    var a = byte.Parse(s.Substring(0, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    var r = byte.Parse(s.Substring(2, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    var g = byte.Parse(s.Substring(4, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    var b = byte.Parse(s.Substring(6, 2), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                    return Color.FromArgb(a, r, g, b);
                }
            }
            catch
            {
                // fall through to ColorConverter for named colors
            }

            try
            {
                var converted = ColorConverter.ConvertFromString(text);
                if (converted is Color named) return named;
            }
            catch
            {
                // return default white
            }

            return Color.FromArgb(255, 255, 255, 255);
        }
    }
}