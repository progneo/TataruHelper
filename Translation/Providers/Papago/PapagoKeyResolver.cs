using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

using Translation.Http;

namespace Translation.Providers.Papago
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

        private const string BrowserUserAgent =
            "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124.0.0.0 Safari/537.36";

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

        private readonly ILogger _logger;
        private readonly string _cachePath;
        private readonly SemaphoreSlim _gate = new SemaphoreSlim(1, 1);

        private string _cachedKey;
        private bool _diskLoadAttempted;

        public PapagoKeyResolver(ILogger logger, string cachePath)
        {
            _logger = logger;
            _cachePath = cachePath;
        }

        public async Task<string> GetKeyAsync(CancellationToken cancellationToken)
        {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
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
                        _logger?.LogInformation("{Message}",
                            $"Papago key resolve: loaded cached key '{diskKey}' from {_cachePath}.");
                        return _cachedKey;
                    }
                }

                _cachedKey = await ResolveAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(_cachedKey))
                    WriteCachedKey(_cachedKey);

                return _cachedKey;
            }
            finally
            {
                _gate.Release();
            }
        }

        public void Invalidate()
        {
            _gate.Wait();
            try
            {
                _cachedKey = null;
                DeleteCachedKey();
            }
            finally
            {
                _gate.Release();
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
                _logger?.LogInformation("{Message}", "Papago key cache read failed: " + ex.Message);
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
                _logger?.LogInformation("{Message}", "Papago key cache write failed: " + ex.Message);
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
                _logger?.LogInformation("{Message}", "Papago key cache delete failed: " + ex.Message);
            }
        }

        private async Task<string> ResolveAsync(CancellationToken cancellationToken)
        {
            try
            {
                var home = await GetStringAsync(HomeUrl, cancellationToken).ConfigureAwait(false);
                if (string.IsNullOrEmpty(home))
                {
                    _logger?.LogInformation("Papago key resolve: homepage fetch failed.");
                    return null;
                }

                foreach (var chunkUrl in EnumerateChunkUrls(home))
                {
                    var chunk = await GetStringAsync(chunkUrl, cancellationToken).ConfigureAwait(false);
                    if (string.IsNullOrEmpty(chunk))
                        continue;

                    var key = ExtractKey(chunk);
                    if (!string.IsNullOrEmpty(key))
                    {
                        _logger?.LogInformation("{Message}",
                            $"Papago key resolve: found key '{key}' in {chunkUrl}.");
                        return key;
                    }
                }

                // Last-chance: the homepage itself sometimes inlines the key.
                var inlineKey = ExtractKey(home);
                if (!string.IsNullOrEmpty(inlineKey))
                {
                    _logger?.LogInformation("{Message}",
                        $"Papago key resolve: found key '{inlineKey}' inline on homepage.");
                    return inlineKey;
                }

                _logger?.LogInformation("Papago key resolve: no HMAC key literal found in homepage or chunks.");
                return null;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger?.LogInformation("{Message}", "Papago key resolve threw: " + ex);
                return null;
            }
        }

        private async Task<string> GetStringAsync(string url, CancellationToken cancellationToken)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Get, url))
            {
                request.Headers.UserAgent.ParseAdd(BrowserUserAgent);
                request.Headers.Accept.ParseAdd("text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8");

                using (var response = await ApiHttpClient.SendAsync(request, cancellationToken)
                           .ConfigureAwait(false))
                {
                    if (!response.IsSuccessStatusCode)
                        return null;

                    return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                }
            }
        }

        private static IEnumerable<string> EnumerateChunkUrls(string html)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var candidates = new List<string>();

            foreach (Match match in ChunkPattern.Matches(html))
            {
                var path = match.Groups["path"].Value;
                if (path.EndsWith("/gtm.js", StringComparison.OrdinalIgnoreCase))
                    continue;

                var url = "https://papago.naver.com" + path;
                if (seen.Add(url))
                    candidates.Add(url);
            }

            // Prefer chunks that empirically contain the HMAC key (main.* and
            // vendors~home.*). Falls back to anything else if those don't match.
            return candidates.OrderByDescending(ChunkPriority);
        }

        private static int ChunkPriority(string url)
        {
            var basename = url.Substring(url.LastIndexOf('/') + 1);

            if (basename.StartsWith("main.", StringComparison.OrdinalIgnoreCase))
                return 3;
            if (basename.StartsWith("vendors~home.", StringComparison.OrdinalIgnoreCase))
                return 2;
            if (basename.StartsWith("runtime~", StringComparison.OrdinalIgnoreCase))
                return -1;

            return 0;
        }

        private static string ExtractKey(string body)
        {
            var match = KeyPattern.Match(body);
            return match.Success ? match.Groups["key"].Value : null;
        }
    }
}