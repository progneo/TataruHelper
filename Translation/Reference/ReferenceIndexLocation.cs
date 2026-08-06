using System;
using System.Globalization;
using System.IO;

using Microsoft.Data.Sqlite;

using Microsoft.Extensions.Logging;

namespace Translation.Reference
{
    /// <summary>
    /// Settles which index of hand-made translations is read.
    ///
    /// There are two: the one the application ships with, and the one the user
    /// fetched. The shipped one is never written to - it belongs to the
    /// installation, and an update replaces the folder it sits in - so updates
    /// go beside the user's settings, where nothing but the user disturbs them.
    /// </summary>
    public static class ReferenceIndexLocation
    {
        /// <summary>
        /// The file to read: whichever of the two was built later.
        ///
        /// Not simply "the user's if there is one". Somebody who fetched an
        /// index in March and installed a release built in June would go on
        /// reading March, and would have no way of telling: both say the same
        /// thing on the General page except for a revision nobody memorises.
        /// An index from before this was recorded counts as the older one,
        /// which it almost certainly is.
        /// </summary>
        public static string Choose(string userPath, string shippedPath, ILogger logger)
        {
            var user = SqliteReferenceTranslationSource.Resolve(userPath);
            var shipped = SqliteReferenceTranslationSource.Resolve(shippedPath);

            var hasUser = user.Length > 0 && File.Exists(user);
            var hasShipped = shipped.Length > 0 && File.Exists(shipped) &&
                             !string.Equals(user, shipped, StringComparison.OrdinalIgnoreCase);

            if (!hasUser)
            {
                return hasShipped ? shipped : user;
            }

            if (!hasShipped)
            {
                return user;
            }

            var userBuilt = BuiltAt(user);
            var shippedBuilt = BuiltAt(shipped);

            if (shippedBuilt <= userBuilt)
            {
                return user;
            }

            logger?.LogInformation(
                "The installed translations ({Shipped:u}) are newer than the fetched ones ({User:u}); using those.",
                shippedBuilt, userBuilt);
            return shipped;
        }

        /// <summary>
        /// When an index was built, or the beginning of time when it does not
        /// say - which is what every index built before this was recorded says.
        /// </summary>
        private static DateTime BuiltAt(string path)
        {
            try
            {
                using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
                {
                    DataSource = path,
                    Mode = SqliteOpenMode.ReadOnly,
                    Pooling = false
                }.ToString());

                connection.Open();

                using var command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM meta WHERE key = 'built'";

                return DateTime.TryParse(command.ExecuteScalar() as string, CultureInfo.InvariantCulture,
                    DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal, out var built)
                    ? built
                    : DateTime.MinValue;
            }
            catch (Exception)
            {
                // Unreadable counts as oldest: the other one is at least known
                // to open, and choosing a file that cannot be read helps nobody.
                return DateTime.MinValue;
            }
        }
    }
}
