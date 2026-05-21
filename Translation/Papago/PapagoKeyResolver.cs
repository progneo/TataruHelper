using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;

using HttpUtilities;

using Translation.HttpUtils;

namespace Translation.Papago
{
    // Pulls the current HMAC key out of the live papago.naver.com bundle.
    //
    // The key is a short literal like "v1.9.3_3bdf0438a8" that Naver rotates
    // every few weeks. It's defined in one of the static JS chunks linked
    // from the homepage. We grab the homepage, list candidate chunk URLs,
    // fetch them, and regex out the literal.
    internal sealed class PapagoKeyResolver
    {
        private const string HomeUrl = "https://papago.naver.com/";

        // Matches strings like "v1.9.3_3bdf0438a8" — semver-ish prefix, underscore, hex suffix.
        // The HMAC key in the bundle is always quoted; the leading/trailing quote anchors it.
        private static readonly Regex KeyPattern = new Regex(
            "[\"']" +
            "(?<key>v\\d+\\.\\d+\\.\\d+_[0-9a-f]{6,})" +
            "[\"']",
            RegexOptions.Compiled);

        // Matches script src / preload href URLs in the homepage HTML.
        // papago.naver.com serves webpack chunks at the root, e.g.
        //   /main.2a5056aff1d7bd1a906a.chunk.js
        //   /vendors~main.edb5a9fac75003f3223f.chunk.js
        //   /runtime~main.82d7fd206b94db71c505.js
        private static readonly Regex ChunkPattern = new Regex(
            "[\"'](?<path>/[A-Za-z0-9~][A-Za-z0-9~_.\\-/]*\\.js)[\"']",
            RegexOptions.Compiled);

        private readonly ILog _logger;
        private readonly string _cachePath;
        private readonly object _gate = new object();

        private string _cachedKey;
        private bool _diskLoadAttempted;

        public PapagoKeyResolver(ILog logger)
            : this(logger, GlobalTranslationSettings.PapagoKeyCachePath)
        {
        }

        public PapagoKeyResolver(ILog logger, string cachePath)
        {
            _logger = logger;
            _cachePath = cachePath;
        }

        public string GetKey()
        {
            lock (_gate)
            {
                if (!string.IsNullOrEmpty(_cachedKey))
                    return _cachedKey;

                if (!_diskLoadAttempted)
                {
                    _diskLoadAttempted = true;
                    var diskKey = ReadCachedKey();
                    if (!string.IsNullOrEmpty(diskKey))
                    {
                        _cachedKey = diskKey;
                        _logger?.WriteLog($"Papago key resolve: loaded cached key '{diskKey}' from {_cachePath}.");
                        return _cachedKey;
                    }
                }

                _cachedKey = Resolve();
                if (!string.IsNullOrEmpty(_cachedKey))
                    WriteCachedKey(_cachedKey);

                return _cachedKey;
            }
        }

        public void Invalidate()
        {
            lock (_gate)
            {
                _cachedKey = null;
                DeleteCachedKey();
            }
        }

        private string ReadCachedKey()
        {
            if (string.IsNullOrEmpty(_cachePath))
                return null;

            try
            {
                if (!File.Exists(_cachePath))
                    return null;

                var contents = File.ReadAllText(_cachePath).Trim();
                return KeyPattern.IsMatch("\"" + contents + "\"") ? contents : null;
            }
            catch (Exception ex)
            {
                _logger?.WriteLog("Papago key cache read failed: " + ex.Message);
                return null;
            }
        }

        private void WriteCachedKey(string key)
        {
            if (string.IsNullOrEmpty(_cachePath))
                return;

            try
            {
                var dir = Path.GetDirectoryName(_cachePath);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    Directory.CreateDirectory(dir);

                File.WriteAllText(_cachePath, key);
            }
            catch (Exception ex)
            {
                _logger?.WriteLog("Papago key cache write failed: " + ex.Message);
            }
        }

        private void DeleteCachedKey()
        {
            if (string.IsNullOrEmpty(_cachePath))
                return;

            try
            {
                if (File.Exists(_cachePath))
                    File.Delete(_cachePath);
            }
            catch (Exception ex)
            {
                _logger?.WriteLog("Papago key cache delete failed: " + ex.Message);
            }
        }

        private string Resolve()
        {
            try
            {
                var reader = CreateReader();

                var home = reader.RequestWebData(HomeUrl, HttpMethods.GET);
                if (!home.IsSuccessful || string.IsNullOrEmpty(home.Body))
                {
                    _logger?.WriteLog("Papago key resolve: homepage fetch failed.");
                    return null;
                }

                foreach (var chunkUrl in EnumerateChunkUrls(home.Body))
                {
                    var chunk = reader.RequestWebData(chunkUrl, HttpMethods.GET);
                    if (!chunk.IsSuccessful || string.IsNullOrEmpty(chunk.Body))
                        continue;

                    var key = ExtractKey(chunk.Body);
                    if (!string.IsNullOrEmpty(key))
                    {
                        _logger?.WriteLog($"Papago key resolve: found key '{key}' in {chunkUrl}.");
                        return key;
                    }
                }

                // Last-chance: the homepage itself sometimes inlines the key.
                var inlineKey = ExtractKey(home.Body);
                if (!string.IsNullOrEmpty(inlineKey))
                {
                    _logger?.WriteLog($"Papago key resolve: found key '{inlineKey}' inline on homepage.");
                    return inlineKey;
                }

                _logger?.WriteLog("Papago key resolve: no HMAC key literal found in homepage or chunks.");
                return null;
            }
            catch (Exception ex)
            {
                _logger?.WriteLog("Papago key resolve threw: " + ex);
                return null;
            }
        }

        private HttpReader CreateReader()
        {
            var reader = new HttpReader(new HttpILogWrapper(_logger));
            TranslationHttpPolicy.ConfigureReader(reader);
            reader.UserAgent = "Mozilla/5.0 (Windows NT 10.0; Win64; x64) " +
                               "AppleWebKit/537.36 (KHTML, like Gecko) " +
                               "Chrome/124.0.0.0 Safari/537.36";
            reader.Accept = "text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8";
            return reader;
        }

        private static IEnumerable<string> EnumerateChunkUrls(string html)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (Match match in ChunkPattern.Matches(html))
            {
                var path = match.Groups["path"].Value;
                if (path.EndsWith("/gtm.js", StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = "https://papago.naver.com" + path;
                if (seen.Add(url))
                    yield return url;
            }
        }

        private static string ExtractKey(string body)
        {
            var match = KeyPattern.Match(body);
            return match.Success ? match.Groups["key"].Value : null;
        }
    }
}