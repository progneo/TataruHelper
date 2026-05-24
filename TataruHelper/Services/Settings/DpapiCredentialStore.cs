using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Text;

using Newtonsoft.Json;

using Translation.Core;
using Translation.Credentials;

namespace FFXIVTataruHelper.Services.Settings
{
    public sealed class DpapiCredentialStore : ITranslationCredentialStore
    {
        private const string SecretsFileName = "Secrets.dat";
        private static readonly byte[] Entropy = Encoding.UTF8.GetBytes("TataruHelper.TranslationSecrets.v1");

        private readonly string _path;
        private readonly object _gate = new object();
        private Dictionary<string, string> _entries;

        public DpapiCredentialStore() : this(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TataruHelper"))
        {
        }

        public DpapiCredentialStore(string directory)
        {
            Directory.CreateDirectory(directory);
            _path = Path.Combine(directory, SecretsFileName);
            _entries = Load();
        }

        public string GetApiKey(TranslationEngineName engine) => Get(Key(engine, "apiKey"));

        public string GetRegion(TranslationEngineName engine) => Get(Key(engine, "region"));

        public string GetModel(TranslationEngineName engine) => Get(Key(engine, "model"));

        public bool IsEngineEnabled(TranslationEngineName engine)
        {
            var raw = Get(Key(engine, "enabled"));
            return !string.Equals(raw, "0", StringComparison.Ordinal);
        }

        public void SetApiKey(TranslationEngineName engine, string apiKey) => Set(Key(engine, "apiKey"), apiKey);

        public void SetRegion(TranslationEngineName engine, string region) => Set(Key(engine, "region"), region);

        public void SetModel(TranslationEngineName engine, string model) => Set(Key(engine, "model"), model);

        public void SetEngineEnabled(TranslationEngineName engine, bool isEnabled)
            => Set(Key(engine, "enabled"), isEnabled ? string.Empty : "0");

        public void Save()
        {
            lock (_gate)
            {
                Persist(_entries);
            }
        }

        private static string Key(TranslationEngineName engine, string field) => engine + ":" + field;

        private string Get(string key)
        {
            lock (_gate)
            {
                string value;
                return _entries.TryGetValue(key, out value) ? value ?? string.Empty : string.Empty;
            }
        }

        private void Set(string key, string value)
        {
            lock (_gate)
            {
                if (string.IsNullOrEmpty(value))
                    _entries.Remove(key);
                else
                    _entries[key] = value;
            }
        }

        private Dictionary<string, string> Load()
        {
            try
            {
                if (!File.Exists(_path))
                    return new Dictionary<string, string>(StringComparer.Ordinal);

                var encrypted = File.ReadAllBytes(_path);
                if (encrypted.Length == 0)
                    return new Dictionary<string, string>(StringComparer.Ordinal);

                var plain = ProtectedData.Unprotect(encrypted, Entropy, DataProtectionScope.CurrentUser);
                var json = Encoding.UTF8.GetString(plain);
                var loaded = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                return loaded ?? new Dictionary<string, string>(StringComparer.Ordinal);
            }
            catch
            {
                return new Dictionary<string, string>(StringComparer.Ordinal);
            }
        }

        private void Persist(Dictionary<string, string> entries)
        {
            try
            {
                var json = JsonConvert.SerializeObject(entries);
                var plain = Encoding.UTF8.GetBytes(json);
                var encrypted = ProtectedData.Protect(plain, Entropy, DataProtectionScope.CurrentUser);
                File.WriteAllBytes(_path, encrypted);
            }
            catch
            {
                // swallow — keys live in-memory for this session even if disk write fails.
            }
        }
    }
}