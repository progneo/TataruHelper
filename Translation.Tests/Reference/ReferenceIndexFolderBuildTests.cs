using System;
using System.IO;

using Microsoft.Data.Sqlite;

using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    // Building from a folder is how a release used to be prepared, by a python
    // script that carried its own copy of the parsing rules. This is the same
    // read on the same rules as the update button, which is the point of it.
    [TestFixture]
    public class ReferenceIndexFolderBuildTests
    {
        private string _root;
        private string _databasePath;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TataruExport_" + Guid.NewGuid().ToString("N"));
            _databasePath = Path.Combine(_root, "index.db");

            WriteSheet("exd/Balloon",
                "The wood... It's watching, you know!",
                "Лес... Он бдит, знаешь ли!");

            // A sheet nobody has begun translating: no English beside it.
            var lonely = Path.Combine(_root, "exd", "Untouched");
            Directory.CreateDirectory(lonely);
            File.WriteAllText(Path.Combine(lonely, "ru.xlf"), Sheet("Что-то"));
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
        public void AnExportOnDisk_BecomesAnIndex()
        {
            var result = new ReferenceIndexUpdater(null)
                .BuildFromFolder(_databasePath, "ru", _root, null);

            Assert.That(result.Outcome, Is.EqualTo(ReferenceUpdateOutcome.Updated));
            Assert.That(result.Lines, Is.EqualTo(1));

            using var source = new SqliteReferenceTranslationSource(_databasePath, null);

            Assert.That(source.TryGetTranslation("The wood... It's watching, you know!", out var translation),
                Is.True);
            Assert.That(translation, Is.EqualTo("Лес... Он бдит, знаешь ли!"));
            Assert.That(source.LanguageCode, Is.EqualTo("ru"));

            // A folder cannot say which commit it was taken at, so an index
            // built this way is one the application will always offer to
            // update rather than one it wrongly believes is current.
            Assert.That(source.Revision, Is.Empty);
        }

        [Test]
        public void AFolderWithNothingInIt_SaysSoRatherThanWritingAnEmptyIndex()
        {
            var empty = Path.Combine(_root, "empty");
            Directory.CreateDirectory(Path.Combine(empty, "exd"));

            var result = new ReferenceIndexUpdater(null)
                .BuildFromFolder(_databasePath, "ru", empty, null);

            Assert.That(result.Outcome, Is.EqualTo(ReferenceUpdateOutcome.Failed));

            // Replacing a working index with an empty one is the worst thing
            // this could do, and a mistyped path is how it would happen.
            Assert.That(File.Exists(_databasePath), Is.False);
        }

        [Test]
        public void AFolderThatIsNotThere_FailsWithoutThrowing()
        {
            var result = new ReferenceIndexUpdater(null)
                .BuildFromFolder(_databasePath, "ru", Path.Combine(_root, "nowhere"), null);

            Assert.That(result.Outcome, Is.EqualTo(ReferenceUpdateOutcome.Failed));
            Assert.That(result.Detail, Is.Not.Empty);
        }

        private void WriteSheet(string folder, string english, string translated)
        {
            var path = Path.Combine(_root, folder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);
            File.WriteAllText(Path.Combine(path, "en.xlf"), Sheet(english));
            File.WriteAllText(Path.Combine(path, "ru.xlf"), Sheet(translated));
        }

        private static string Sheet(string text)
        {
            return "<xliff><file><body><trans-unit id=\"8\"><source>8</source>" +
                   "<target state=\"final\">" + text + "</target></trans-unit></body></file></xliff>";
        }
    }
}
