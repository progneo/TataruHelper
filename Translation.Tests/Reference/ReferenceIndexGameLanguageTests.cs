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
    // FFXIV ships in English, German, French and Japanese, and a line is read
    // off the screen in whichever of those the game is set to. The index is
    // keyed on one of them, so it can only answer a client playing in that one -
    // for everybody else it has to stand aside and let an engine work, rather
    // than sit there matching nothing while the user wonders why.
    [TestFixture]
    public class ReferenceIndexGameLanguageTests
    {
        private string _root;

        [SetUp]
        public void SetUp()
        {
            _root = Path.Combine(Path.GetTempPath(), "TataruGameLang_" + Guid.NewGuid().ToString("N"));

            // The same row, as the game words it in each language, and as the
            // translators render it.
            WriteSheet("exd/Balloon", new Dictionary<string, string>
            {
                ["en"] = "The wood... It's watching, you know!",
                ["de"] = "Der Wald... Er beobachtet uns!",
                ["ru"] = "Лес... Он бдит, знаешь ли!"
            });
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
        public void AnIndexForAGermanClient_IsKeyedOnTheGermanLine()
        {
            var path = Build("de", "ru");

            using var source = new SqliteReferenceTranslationSource(path, null);

            Assert.That(source.SourceLanguageCode, Is.EqualTo("de"));
            Assert.That(source.LanguageCode, Is.EqualTo("ru"));

            Assert.That(source.TryGetTranslation("Der Wald... Er beobachtet uns!", out var translation), Is.True);
            Assert.That(translation, Is.EqualTo("Лес... Он бдит, знаешь ли!"));

            // The English line is not in it at all - that column was never read.
            Assert.That(source.TryGetTranslation("The wood... It's watching, you know!", out _), Is.False);
        }

        [Test]
        public async Task AGermanClient_ReadsFromAGermanIndex()
        {
            var translator = CreateTranslator(Build("de", "ru"));

            Assert.That(await TranslateAsync(translator, "Der Wald... Er beobachtet uns!", "de"),
                Is.EqualTo("Лес... Он бдит, знаешь ли!"));
        }

        [Test]
        public async Task AnEnglishIndex_StandsAsideOnAGermanClient()
        {
            // Before the game's language was part of this, an English index was
            // consulted whatever the client was set to. It matched nothing, and
            // nothing said why: the translations simply appeared not to work.
            var translator = CreateTranslator(Build("en", "ru"));

            Assert.That(await TranslateAsync(translator, "Der Wald... Er beobachtet uns!", "de"),
                Is.EqualTo("engine"));
        }

        [Test]
        public async Task AGermanIndex_StandsAsideOnAnEnglishClient()
        {
            var translator = CreateTranslator(Build("de", "ru"));

            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!", "en"),
                Is.EqualTo("engine"));
        }

        [Test]
        public async Task DetectTheLanguage_LeavesTheIndexToTry()
        {
            // The window's language can be left on "Auto", and then nobody has
            // said what the game is in. Refusing the index there would take the
            // translations away from everyone who never touched the setting -
            // and it cannot go wrong, because the lookup is by exact text.
            var translator = CreateTranslator(Build("en", "ru"));

            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!", "auto"),
                Is.EqualTo("Лес... Он бдит, знаешь ли!"));
        }

        [Test]
        public async Task SomebodyWritingGermanInChat_DoesNotDecideWhatTheGameIsIn()
        {
            // The window detects the language of each line, and a German player
            // typing in chat is detected as German. The language of the client
            // is a setting, not a property of one line: taking the guess for an
            // answer would put the index away for the next line of dialogue.
            var translator = CreateTranslator(Build("en", "ru"), detectedAs: "German");

            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!", "auto"),
                Is.EqualTo("Лес... Он бдит, знаешь ли!"));
        }

        [Test]
        public async Task WhatTheGameSays_OutranksTheWindow()
        {
            // The window is set to work the language out for itself and gets it
            // wrong on this line; the game has already said it is English.
            var translator = CreateTranslator(Build("en", "ru"), detectedAs: "German");
            translator.GameLanguage = "en";

            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!", "auto"),
                Is.EqualTo("Лес... Он бдит, знаешь ли!"));
        }

        [Test]
        public async Task AnIndexForAnotherClient_StandsAsideEvenIfTheWindowDisagrees()
        {
            // The game is in German, so a line is read in German; an English
            // index cannot answer it whatever the window is set to.
            var translator = CreateTranslator(Build("en", "ru"));
            translator.GameLanguage = "de";

            Assert.That(await TranslateAsync(translator, "The wood... It's watching, you know!", "en"),
                Is.EqualTo("engine"));
        }

        [Test]
        public void AWindowSetToWorkItOut_IsNotALanguageToBuildFor()
        {
            // "auto" reached the builder once and sent it looking for auto.xlf,
            // which cost a full download before it could say it found nothing.
            Assert.That(ReferenceIndexUpdater.IsGameLanguage("auto"), Is.False);
            Assert.That(ReferenceIndexUpdater.ResolveGameLanguage("auto", "de"), Is.EqualTo("de"));
            Assert.That(ReferenceIndexUpdater.ResolveGameLanguage("auto", "auto"), Is.EqualTo("en"));
            Assert.That(ReferenceIndexUpdater.ResolveGameLanguage("ja", "de"), Is.EqualTo("ja"));
        }

        [Test]
        public async Task AnUnusableLanguage_IsRefusedBeforeAnythingIsDownloaded()
        {
            // No network is touched: the pair is impossible, and saying so
            // after several hundred megabytes is how this was found out.
            var result = await new ReferenceIndexUpdater(null).UpdateAsync(
                Path.Combine(_root, "never-written.db"), "auto", "ru", string.Empty, null, null,
                CancellationToken.None);

            Assert.That(result.Outcome, Is.EqualTo(ReferenceUpdateOutcome.Failed));
            Assert.That(result.Detail, Does.Contain("auto"));
            Assert.That(File.Exists(Path.Combine(_root, "never-written.db")), Is.False);
        }

        [Test]
        public void AnIndexFromBeforeThisWasRecorded_CountsAsEnglish()
        {
            // Every index built until now was keyed on the English column, and
            // says nothing about it. Read as anything else, it would answer an
            // English client with silence.
            var path = Path.Combine(_root, "old.db");
            WriteIndexWithoutSourceLanguage(path);

            using var source = new SqliteReferenceTranslationSource(path, null);

            Assert.That(source.SourceLanguageCode, Is.EqualTo("en"));
        }

        private string Build(string gameLanguage, string readingLanguage)
        {
            var path = Path.Combine(_root, gameLanguage + "-" + readingLanguage + ".db");
            var result = new ReferenceIndexUpdater(null)
                .BuildFromFolder(path, gameLanguage, readingLanguage, _root, null);

            Assert.That(result.Outcome, Is.EqualTo(ReferenceUpdateOutcome.Updated), "built " + path);
            return path;
        }

        private static WebTranslator CreateTranslator(string databasePath, string detectedAs = null)
        {
            var settings = new TranslationSettings { ReferenceTranslationsPath = databasePath };

            return new WebTranslator(NullLogger.Instance, new[] { new EngineProvider() }, settings,
                detectedAs == null ? null : _ => detectedAs)
            {
                UseReferenceTranslations = true
            };
        }

        private static async Task<string> TranslateAsync(WebTranslator translator, string sentence, string gameLanguage)
        {
            var result = await translator.TranslateAsync(
                sentence,
                new TranslationEngine(TranslationEngineName.GoogleTranslate,
                    new List<TranslatorLanguage>
                    {
                        new TranslatorLanguage("English", "English", "en"),
                        new TranslatorLanguage("German", "German", "de"),
                        new TranslatorLanguage("Russian", "Russian", "ru")
                    },
                    10),
                gameLanguage == "auto"
                    ? new TranslatorLanguage("Auto", "Auto", "auto")
                    : new TranslatorLanguage(gameLanguage, gameLanguage, gameLanguage),
                new TranslatorLanguage("Russian", "Russian", "ru"));

            return result.Text;
        }

        private void WriteSheet(string folder, Dictionary<string, string> byLanguage)
        {
            var path = Path.Combine(_root, folder.Replace('/', Path.DirectorySeparatorChar));
            Directory.CreateDirectory(path);

            foreach (var pair in byLanguage)
            {
                File.WriteAllText(Path.Combine(path, pair.Key + ".xlf"),
                    "<xliff><file><body><trans-unit id=\"8\"><source>8</source>" +
                    "<target state=\"final\">" + pair.Value + "</target></trans-unit></body></file></xliff>");
            }
        }

        private static void WriteIndexWithoutSourceLanguage(string path)
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
            create.CommandText =
                "CREATE TABLE line (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);" +
                "INSERT INTO meta VALUES ('language', 'ru'), ('lines', '1');";
            create.ExecuteNonQuery();
        }

        /// <summary>Answers whatever the index did not, so the two are told apart.</summary>
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
