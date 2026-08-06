using System;
using System.IO;

using Microsoft.Extensions.Logging;

namespace Translation.Reference
{
    /// <summary>
    /// Settles where the index of hand-made translations lives.
    ///
    /// It lives with the user's settings rather than with the application. The
    /// application is replaced wholesale when it updates, and an index that sat
    /// beside it went with it - so an update quietly took back translations the
    /// user had fetched, and said nothing about it.
    /// </summary>
    public static class ReferenceIndexLocation
    {
        /// <summary>
        /// The file to read, having moved one left beside the application into
        /// place first.
        ///
        /// Earlier versions shipped the index inside the application and wrote
        /// updates there. Those installations would otherwise come up empty
        /// after this change and fetch several hundred megabytes they already
        /// have. Moved rather than copied: one index, in one place, and the
        /// question does not arise again.
        /// </summary>
        public static string Prepare(string userPath, string legacyPath, ILogger logger)
        {
            var resolved = SqliteReferenceTranslationSource.Resolve(userPath);

            if (resolved.Length == 0 || File.Exists(resolved))
            {
                return resolved;
            }

            var legacy = SqliteReferenceTranslationSource.Resolve(legacyPath);
            if (legacy.Length == 0 ||
                string.Equals(legacy, resolved, StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(legacy))
            {
                return resolved;
            }

            try
            {
                var directory = Path.GetDirectoryName(resolved);
                if (!string.IsNullOrEmpty(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                File.Move(legacy, resolved);
                logger?.LogInformation("Reference translations moved from {Legacy} to {Path}.", legacy, resolved);
                return resolved;
            }
            catch (Exception ex)
            {
                // Reading what is there beats reading nothing. An update will
                // write to the settled path, and that is what the next start
                // will find.
                logger?.LogInformation("{Message}", Convert.ToString(ex));
                return legacy;
            }
        }
    }
}
