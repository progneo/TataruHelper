using System;
using System.Globalization;
using System.IO;

using Microsoft.Data.Sqlite;

using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    // There are two indexes: the one the application was installed with, and the
    // one the user fetched. The installed one is never written to - an update
    // replaces the folder it sits in - so updates go beside the user's settings,
    // and something has to say which of the two is read.
    [TestFixture]
    public class ReferenceIndexLocationTests
    {
        private string _root;
        private string _userPath;
        private string _shippedPath;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TataruWhich_" + Guid.NewGuid().ToString("N"));
            _userPath = Path.Combine(_root, "user", "ReferenceTranslations.db");
            _shippedPath = Path.Combine(_root, "app", "Resources", "ReferenceTranslations.db");
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void NothingFetched_ReadsTheOneInstalledWithTheApplication()
        {
            // A fresh install translates from the first line, without waiting
            // for anything to be downloaded.
            WriteIndex(_shippedPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_shippedPath)));
        }

        [Test]
        public void AFetchedIndex_WinsOverTheInstalledOne()
        {
            WriteIndex(_shippedPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            WriteIndex(_userPath, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_userPath)));
        }

        [Test]
        public void ANewerRelease_WinsOverAnOlderFetch()
        {
            // Fetched in January, installed a release built in June. Preferring
            // the fetched one on principle would leave five months of
            // translations unread, and nothing on screen would say why.
            WriteIndex(_userPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));
            WriteIndex(_shippedPath, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_shippedPath)));
        }

        [Test]
        public void AnIndexFromBeforeThisWasRecorded_CountsAsTheOlderOne()
        {
            // Every index built before the build date was written down, which
            // is every index anybody has today.
            WriteIndex(_userPath, null);
            WriteIndex(_shippedPath, new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_shippedPath)));
        }

        [Test]
        public void TwoUndatedIndexes_LeaveTheFetchedOneInPlace()
        {
            // Neither can be shown to be newer, and the fetched one is the one
            // somebody asked for.
            WriteIndex(_userPath, null);
            WriteIndex(_shippedPath, null);

            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_userPath)));
        }

        [Test]
        public void AnUnreadableFile_DoesNotWinAnything()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_userPath));
            File.WriteAllText(_userPath, "not a database");
            WriteIndex(_shippedPath, new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc));

            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_shippedPath)));
        }

        [Test]
        public void NoIndexAtAll_StillNamesWhereAFetchWouldGo()
        {
            Assert.That(Choose(), Is.EqualTo(Path.GetFullPath(_userPath)));
        }

        [Test]
        public void AnEnvironmentVariable_IsExpanded()
        {
            // The settings name the user's folder as %APPDATA%, since this
            // project has no business knowing what it is called.
            var resolved = SqliteReferenceTranslationSource.Resolve(
                "%APPDATA%/TataruHelper/ReferenceTranslations.db");

            Assert.That(resolved, Does.Not.Contain("%APPDATA%"));
            Assert.That(Path.IsPathRooted(resolved), Is.True);
            Assert.That(resolved, Does.EndWith("ReferenceTranslations.db"));
        }

        private string Choose()
        {
            return ReferenceIndexLocation.Choose(_userPath, _shippedPath, null);
        }

        private static void WriteIndex(string path, DateTime? built)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));

            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());

            connection.Open();

            using var create = connection.CreateCommand();
            create.CommandText = "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);" +
                                 "INSERT INTO meta VALUES ('language', 'ru');";
            create.ExecuteNonQuery();

            if (!built.HasValue)
            {
                return;
            }

            using var stamp = connection.CreateCommand();
            stamp.CommandText = "INSERT INTO meta VALUES ('built', $built)";
            var parameter = stamp.CreateParameter();
            parameter.ParameterName = "$built";
            parameter.Value = built.Value.ToString("O", CultureInfo.InvariantCulture);
            stamp.Parameters.Add(parameter);
            stamp.ExecuteNonQuery();
        }
    }
}
