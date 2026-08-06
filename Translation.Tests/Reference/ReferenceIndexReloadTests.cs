using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using Translation.Models;
using Translation.Providers;
using Translation.Reference;
using Translation.Settings;

namespace Translation.Tests.Reference
{
    // The index is rebuilt from a translation project that keeps working, and
    // the rebuilt file has to reach a running application. Restarting to pick it
    // up would be a poor answer: the reason it is rebuilt at all is that
    // somebody is playing and wants the newer lines now.
    [TestFixture]
    public class ReferenceIndexReloadTests
    {
        private string _databasePath;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), "TataruReload_" + Guid.NewGuid().ToString("N") + ".db");
            WriteIndex(_databasePath, "The wood... It's watching, you know!", "Лес... Он бдит, знаешь ли!", string.Empty);
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            foreach (var path in new[] { _databasePath, _databasePath + ".replacement" })
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }

        [Test]
        public async Task ReopenedIndex_AnswersFromTheNewFile()
        {
            var translator = CreateTranslator();

            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!"),
                Is.EqualTo("Лес... Он бдит, знаешь ли!"));

            ReplaceIndexWhileInUse(translator,
                "I am Hydaelyn. All made one.", "Я — Хайделин. Множество в Одном.", "abc1234");

            Assert.That(await TranslateAsync(translator, "I am Hydaelyn. All made one."),
                Is.EqualTo("Я — Хайделин. Множество в Одном."));

            // The line the old index knew is gone with it, rather than being
            // answered from something still held open.
            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!"),
                Is.EqualTo("engine"));
        }

        [Test]
        public void ReopenedIndex_ReportsWhatItNowHolds()
        {
            var translator = CreateTranslator();

            // Nothing recorded the revision of the index the application ships
            // with, so there is nothing to compare against and the first update
            // always downloads.
            Assert.That(translator.ReferenceIndexRevision, Is.Empty);
            Assert.That(translator.ReferenceIndexLines, Is.EqualTo(1));
            Assert.That(translator.ReferenceIndexLanguage, Is.EqualTo("ru"));

            ReplaceIndexWhileInUse(translator, "Hello.", "Здравствуй.", "abc1234");

            Assert.That(translator.ReferenceIndexRevision, Is.EqualTo("abc1234"));
        }

        [Test]
        public async Task ReopenedIndex_StillKnowsWhoIsPlaying()
        {
            var translator = CreateTranslator();
            translator.PlayerName = "D'ark One";

            ReplaceIndexWhileInUse(translator, "Hello.", "Здравствуй.", "abc1234");

            // The name and gender were read from the game once. Nothing will
            // announce them again while the session lasts, so a reopened index
            // that forgot them would leave every line addressed to the player
            // going to an engine until the game was restarted.
            Assert.That(translator.PlayerName, Is.EqualTo("D'ark One"));
            Assert.That(await TranslateAsync(translator, "Go swiftly, D'ark."),
                Is.EqualTo("Поторопись, D'ark."));
        }

        /// <summary>
        /// Ends an update the way the updater ends one: writes the new index,
        /// takes the old one out of the application's hands, swaps them, and
        /// opens what is now there.
        ///
        /// Goes through the updater's own code rather than writing the file
        /// here. An earlier version of this test wrote the replacement itself,
        /// with pooling turned off in the test - which is exactly what the
        /// updater was missing. The test passed and the first real update lost
        /// a finished index to a handle the writer had left open.
        /// </summary>
        private void ReplaceIndexWhileInUse(WebTranslator translator,
            string source, string translated, string revision)
        {
            var builder = new ReferenceIndexBuilder();
            builder.AddSheet("exd/Test", Sheet(source), Sheet(translated));
            // The markup the game fills the character's name into.
            const string playerName = "&lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;";
            builder.AddSheet("exd/Addressed",
                Sheet("Go swiftly, " + playerName + "."),
                Sheet("Поторопись, " + playerName + "."));

            ReferenceIndexUpdater.WriteAndInstall(_databasePath, builder, "ru", revision, "test",
                translator.CloseReferenceIndex);

            translator.ReopenReferenceIndex();
        }

        private static string Sheet(string text)
        {
            return "<xliff><file><body><trans-unit id=\"1\"><source>1</source>" +
                   "<target state=\"final\">" + text + "</target></trans-unit></body></file></xliff>";
        }

        private WebTranslator CreateTranslator()
        {
            var settings = new TranslationSettings { ReferenceTranslationsPath = _databasePath };
            var provider = new EngineProvider();

            return new WebTranslator(NullLogger.Instance, new[] { provider }, settings)
            {
                UseReferenceTranslations = true
            };
        }

        private static async Task<string> TranslateAsync(WebTranslator translator, string sentence)
        {
            var result = await translator.TranslateAsync(
                sentence,
                new TranslationEngine(TranslationEngineName.GoogleTranslate,
                    new List<TranslatorLanguage>
                    {
                        new TranslatorLanguage("English", "English", "en"),
                        new TranslatorLanguage("Russian", "Russian", "ru")
                    },
                    10),
                new TranslatorLanguage("English", "English", "en"),
                new TranslatorLanguage("Russian", "Russian", "ru"));

            return result.Text;
        }

        private static void WriteIndex(string path, string source, string translated, string revision)
        {
            using var connection = new SqliteConnection(new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Pooling = false
            }.ToString());

            connection.Open();

            using var create = connection.CreateCommand();
            create.CommandText =
                "CREATE TABLE line (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                "CREATE TABLE pattern (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                "CREATE TABLE speaker (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                "CREATE TABLE gendered (source TEXT NOT NULL, feminine INTEGER NOT NULL, " +
                "translated TEXT NOT NULL, PRIMARY KEY (feminine, source)) WITHOUT ROWID;" +
                "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);" +
                "INSERT INTO line VALUES ($source, $translated);" +
                "INSERT INTO pattern VALUES ('Go swiftly, ' || char(1) || '.', " +
                "'Поторопись, ' || char(1) || '.');" +
                "INSERT INTO meta VALUES ('language', 'ru'), ('lines', '1'), ('revision', $revision);";

            Bind(create, "$source", source);
            Bind(create, "$translated", translated);
            Bind(create, "$revision", revision);
            create.ExecuteNonQuery();
        }

        private static void Bind(SqliteCommand command, string name, string value)
        {
            var parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value;
            command.Parameters.Add(parameter);
        }

        /// <summary>Answers everything the index did not, so the two are told apart.</summary>
        private sealed class EngineProvider : ITranslationProvider
        {
            public TranslationEngineName EngineName => TranslationEngineName.GoogleTranslate;

            public Task<string> TranslateAsync(string sentence, string inLang, string outLang,
                CancellationToken cancellationToken)
            {
                return Task.FromResult("engine");
            }
        }
    }
}
