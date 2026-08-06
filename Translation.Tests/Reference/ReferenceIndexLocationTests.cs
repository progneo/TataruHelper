using System;
using System.IO;

using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    // The index lives with the user's settings, not with the application, so
    // that updating the application does not take back translations the user
    // fetched. Installations made before that decision have one beside the
    // application, and they are not to be made to download it again.
    [TestFixture]
    public class ReferenceIndexLocationTests
    {
        private string _root;
        private string _userPath;
        private string _legacyPath;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TataruWhere_" + Guid.NewGuid().ToString("N"));
            _userPath = Path.Combine(_root, "user", "ReferenceTranslations.db");
            _legacyPath = Path.Combine(_root, "app", "Resources", "ReferenceTranslations.db");
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_root, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void AnIndexBesideTheApplication_MovesToTheUserFolder()
        {
            Write(_legacyPath, "an index");

            var chosen = ReferenceIndexLocation.Prepare(_userPath, _legacyPath, null);

            Assert.That(chosen, Is.EqualTo(Path.GetFullPath(_userPath)));
            Assert.That(File.ReadAllText(_userPath), Is.EqualTo("an index"));

            // Moved, not copied: two copies of eighty megabytes, one of which
            // silently shadows the other, is the arrangement this avoids.
            Assert.That(File.Exists(_legacyPath), Is.False);
        }

        [Test]
        public void AnIndexAlreadyInTheUserFolder_IsLeftAlone()
        {
            Write(_userPath, "the one in use");
            Write(_legacyPath, "an older one left behind");

            var chosen = ReferenceIndexLocation.Prepare(_userPath, _legacyPath, null);

            Assert.That(chosen, Is.EqualTo(Path.GetFullPath(_userPath)));
            Assert.That(File.ReadAllText(_userPath), Is.EqualTo("the one in use"));
        }

        [Test]
        public void NoIndexAnywhere_NamesWhereOneWouldGo()
        {
            // A fresh install: nothing is downloaded until the button is
            // pressed, and the path still has to be the one an update writes.
            var chosen = ReferenceIndexLocation.Prepare(_userPath, _legacyPath, null);

            Assert.That(chosen, Is.EqualTo(Path.GetFullPath(_userPath)));
            Assert.That(File.Exists(chosen), Is.False);
        }

        [Test]
        public void TheSamePathTwice_IsNotMovedOntoItself()
        {
            Write(_userPath, "an index");

            var chosen = ReferenceIndexLocation.Prepare(_userPath, _userPath, null);

            Assert.That(chosen, Is.EqualTo(Path.GetFullPath(_userPath)));
            Assert.That(File.ReadAllText(_userPath), Is.EqualTo("an index"));
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

        private static void Write(string path, string content)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(path, content);
        }
    }
}
