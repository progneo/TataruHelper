using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;

using FFXIVTataruHelper.Utils;
using FFXIVTataruHelper.WinUtils;

using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FFXIVTataruHelper
{
    static class Helper
    {
        private static readonly JsonSerializerSettings JsonSettings = new JsonSerializerSettings
        {
            Formatting = Formatting.Indented, Converters = { new MediaColorJsonConverter() },
        };

        public static T LoadJsonData<T>(string path)
        {
            T result = (T)Activator.CreateInstance(typeof(T));

            try
            {
                using (TextReader reader = new StreamReader(path))
                {
                    result = JsonConvert.DeserializeObject<T>(reader.ReadToEnd(), JsonSettings);
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog(Convert.ToString(e));

                try
                {
                    using (TextWriter writer = new StreamWriter(path))
                    {
                        writer.WriteLine(JsonConvert.SerializeObject(result, JsonSettings));
                    }
                }
                catch (Exception e1)
                {
                    Logger.WriteLog(Convert.ToString(e1));
                }
            }

            return result;
        }

        public static void SaveJson(object obj, string path)
        {
            try
            {
                using (TextWriter writer = new StreamWriter(path))
                {
                    writer.WriteLine(JsonConvert.SerializeObject(obj, JsonSettings));
                    writer.Flush();
                }
            }
            catch (Exception e)
            {
                Logger.WriteLog(Convert.ToString(e));
            }
        }

        public static T GetLast<T>(this IList<T> list)
        {
            if (list == null)
                throw new ArgumentNullException("list");
            if (list.Count == 0)
                throw new ArgumentException(
                    "Cannot get last item because the list is empty");

            int lastIdx = list.Count - 1;
            return list[lastIdx];
        }

        public static bool TryAdd<TKey, TValue>(this Dictionary<TKey, TValue> dict, TKey key, TValue value)
        {
            if (dict.ContainsKey(key)) return false;

            dict.Add(key, value);

            return true;
        }

        public static IList<T> Swap<T>(this IList<T> list, int indexA, int indexB)
        {
            (list[indexA], list[indexB]) = (list[indexB], list[indexA]);
            return list;
        }

        public static Key RealKey(this KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.System:
                    return e.SystemKey;

                case Key.ImeProcessed:
                    return e.ImeProcessedKey;

                case Key.DeadCharProcessed:
                    return e.DeadCharProcessedKey;

                default:
                    return e.Key;
            }
        }

        public static void Unminimize(Window window)
        {
            var hwnd = (HwndSource.FromVisual(window) as HwndSource).Handle;
            Win32Interfaces.ShowWindow(hwnd, Win32Interfaces.ShowWindowCommands.Restore);
        }

        public static void Unminimize(IntPtr window)
        {
            var hwnd = window;
            Win32Interfaces.ShowWindow(hwnd, Win32Interfaces.ShowWindowCommands.Restore);
        }

        public static string ClearBlackListString(string text)
        {
            return text;
        }

        public static bool IsStringLettersEqual(string str1, string str2)
        {
            String onlyLetters1 = new String(str1.Where(Char.IsLetter).ToArray());
            String onlyLetters2 = new String(str2.Where(Char.IsLetter).ToArray());

            return onlyLetters1 == onlyLetters2;
        }

        public static string Base64Encode(string plainText)
        {
            var plainTextBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainTextBytes);
        }

        public static string Base64Decode(string base64EncodedData)
        {
            var base64EncodedBytes = Convert.FromBase64String(base64EncodedData);
            return Encoding.UTF8.GetString(base64EncodedBytes);
        }

        public static string GetHash(HashAlgorithm hashAlgorithm, string input)
        {
            // Convert the input string to a byte array and compute the hash.
            byte[] data = hashAlgorithm.ComputeHash(Encoding.UTF8.GetBytes(input));

            // Create a new Stringbuilder to collect the bytes
            // and create a string.
            var sBuilder = new StringBuilder();

            // Loop through each byte of the hashed data 
            // and format each one as a hexadecimal string.
            for (int i = 0; i < data.Length; i++)
            {
                sBuilder.Append(data[i].ToString("x2"));
            }

            // Return the hexadecimal string.
            return sBuilder.ToString();
        }

        // Verify a hash against a string.
        public static bool VerifyHash(HashAlgorithm hashAlgorithm, string input, string hash)
        {
            // Hash the input.
            var hashOfInput = GetHash(hashAlgorithm, input);

            // Create a StringComparer an compare the hashes.
            StringComparer comparer = StringComparer.OrdinalIgnoreCase;

            return comparer.Compare(hashOfInput, hash) == 0;
        }

        public static string ConvertISOLanguageNameToSystemName(string lang)
        {
            string result = string.Empty;
            switch (lang)
            {
                case "eng":
                    result = "English";
                    break;
                case "dan":
                    result = "Danish";
                    break;
                case "nor":
                    result = "Norwegian";
                    break;
                case "fra":
                    result = "French";
                    break;
                case "spa":
                    result = "Spanish";
                    break;
                case "swe":
                    result = "Swedish";
                    break;
                case "nld":
                    result = "Dutch";
                    break;
                case "ita":
                    result = "Italian";
                    break;
                case "por":
                    result = "Portuguese";
                    break;
                case "deu":
                    result = "German";
                    break;
                case "rus":
                    result = "Russian";
                    break;
                case "kor":
                    result = "Korean";
                    break;
                case "zho":
                    result = "Chinese";
                    break;
                case "jpn":
                    result = "Japanese";
                    break;
            }

            return result;
        }
    }
}