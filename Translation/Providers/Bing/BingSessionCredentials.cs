using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace Translation.Providers.Bing
{
    /// <summary>
    /// What the translator page hands its own JavaScript so the browser may call
    /// the endpoint behind it. There is no key to sign up for: the page issues
    /// these on load and they expire, so they are read the same way the page
    /// itself would and re-read when they run out.
    /// </summary>
    internal sealed class BingSessionCredentials
    {
        public BingSessionCredentials(string ig, string iid, string key, string token, DateTime expiresAtUtc)
        {
            Ig = ig;
            Iid = iid;
            Key = key;
            Token = token;
            ExpiresAtUtc = expiresAtUtc;
        }

        public string Ig { get; }

        public string Iid { get; }

        public string Key { get; }

        public string Token { get; }

        public DateTime ExpiresAtUtc { get; }

        /// <summary>
        /// Counted as spent a minute early. The page states its own lifetime, and
        /// a request that leaves just before the edge arrives just after it.
        /// </summary>
        public bool IsUsableAt(DateTime utcNow) => utcNow < ExpiresAtUtc - TimeSpan.FromMinutes(1);

        private static readonly Regex IgPattern =
            new Regex(@"IG:""([^""]+)""", RegexOptions.Compiled);

        private static readonly Regex IidPattern =
            new Regex(@"data-iid=""([^""]+)""", RegexOptions.Compiled);

        /// <summary>
        /// The page declares these as a JavaScript array of
        /// [issued-at, token, lifetime-in-milliseconds].
        /// </summary>
        private static readonly Regex AbusePreventionPattern = new Regex(
            @"params_AbusePreventionHelper\s*=\s*\[\s*(\d+)\s*,\s*""([^""]+)""\s*,\s*(\d+)\s*\]",
            RegexOptions.Compiled);

        /// <summary>
        /// Returns null when the page is not the one we expect - Bing served a
        /// consent wall, a redirect, or changed its markup. The caller treats
        /// that as "no translation this time" rather than as an error, because
        /// there is nothing the player could do about it either way.
        /// </summary>
        public static BingSessionCredentials TryParse(string html, DateTime utcNow)
        {
            if (string.IsNullOrWhiteSpace(html))
                return null;

            var ig = IgPattern.Match(html);
            var iid = IidPattern.Match(html);
            var abuse = AbusePreventionPattern.Match(html);

            if (!ig.Success || !iid.Success || !abuse.Success)
                return null;

            if (!long.TryParse(abuse.Groups[3].Value, NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var lifetimeMs) || lifetimeMs <= 0)
            {
                return null;
            }

            return new BingSessionCredentials(
                ig.Groups[1].Value,
                iid.Groups[1].Value,
                abuse.Groups[1].Value,
                abuse.Groups[2].Value,
                utcNow.AddMilliseconds(lifetimeMs));
        }
    }
}
