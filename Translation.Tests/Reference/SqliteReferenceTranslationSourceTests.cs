using System;
using System.IO;

using Microsoft.Data.Sqlite;

using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    // The index holds translations somebody made by hand for the game's own
    // dialogue. What matters is that a line read off the screen finds its entry
    // despite the game having wrapped it across lines, and that a line nobody
    // has translated says so rather than answering with something.
    [TestFixture]
    public class SqliteReferenceTranslationSourceTests
    {
        private string _databasePath;

        [SetUp]
        public void SetUp()
        {
            _databasePath = Path.Combine(Path.GetTempPath(), "TataruReference_" + Guid.NewGuid().ToString("N") + ".db");

            using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
            {
                connection.Open();

                using (var create = connection.CreateCommand())
                {
                    create.CommandText =
                        "CREATE TABLE line (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                        "CREATE TABLE pattern (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                        "CREATE TABLE speaker (source TEXT PRIMARY KEY, translated TEXT NOT NULL) WITHOUT ROWID;" +
                        "CREATE TABLE gendered (source TEXT NOT NULL, feminine INTEGER NOT NULL, translated TEXT NOT NULL, PRIMARY KEY (feminine, source)) WITHOUT ROWID;" +
                        "INSERT INTO speaker VALUES ('Mother Miounne', 'Матушка Миунна'), ('Y''shtola', 'Я''штола'), " +
                        "('Sahjattra Concern representative', 'Представитель «Саджаттры»');" +
                        "INSERT INTO gendered VALUES " +
                        "('This position is yours, adventurer.', 1, 'Искательница приключений, на позицию.')," +
                        "('This position is yours, adventurer.', 0, 'Искатель приключений, на позицию.')," +
                        "('Hydaelyn would speak to this woman...', 1, 'Хайделин говорила с этой женщиной...')," +
                        "('Hydaelyn would speak to this man...', 0, 'Хайделин говорила с этим мужчиной...');" +
                        "CREATE TABLE gendered_pattern (source TEXT NOT NULL, feminine INTEGER NOT NULL, " +
                        "translated TEXT NOT NULL, PRIMARY KEY (feminine, source)) WITHOUT ROWID;" +
                        "INSERT INTO gendered_pattern VALUES " +
                        "('Are you ready, ' || char(1) || '?', 1, char(1) || ', ты готова?')," +
                        "('Are you ready, ' || char(1) || '?', 0, char(1) || ', ты готов?');" +
                        "CREATE TABLE meta (key TEXT PRIMARY KEY, value TEXT NOT NULL);" +
                        "INSERT INTO meta VALUES ('language', 'ru');" +
                        "INSERT INTO line VALUES " +
                        "('The wood... It''s watching, you know!', 'Лес... Он бдит, знаешь ли!')," +
                        "('I am Hydaelyn. All made one.', 'Я — Хайделин. Множество в Одном.')," +
                        "('When you clashed with him', " +
                        "'когда ты с ним <var 08 E905 ((схлестнулась)) ((схлестнулся)) /var>')," +
                        "('<sigh> Here we go again.', '<sigh> Эх... Опять двадцать пять.');" +
                        "INSERT INTO pattern VALUES " +
                        "('The fate of Gridania hangs in the balance. Go swiftly, ' || char(1) || '.', " +
                        "'Судьба Гридании висит на волоске. Поторопись, ' || char(1) || '.'), " +
                        // Named on one side only, which is the common shape
                        // once the game is played in something but English.
                        "('Und er wird deine Hilfe benötigen.', " +
                        "'Поторопись, ' || char(1) || '.'), " +
                        "('Well met, ' || char(1) || '.', 'Приветствую.');";
                    create.ExecuteNonQuery();
                }
            }

            SqliteConnection.ClearAllPools();
        }

        [TearDown]
        public void TearDown()
        {
            SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(_databasePath);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void ASpeakerTheGameCapitalises_IsStillFound()
        {
            // The sheet writes a name as it would sit in a sentence; the game
            // draws it with every word capitalised. Nearly half the names in
            // the index differ that way, and none of them were being found.
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetSpeakerName("Sahjattra Concern Representative", out var translated),
                    Is.True);
                Assert.That(translated, Is.EqualTo("Представитель «Саджаттры»"));
            }
        }

        [Test]
        public void ALineNamingAndAgreeing_NeedsBothFactsAndThenAnswers()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                // Neither fact known, then one, then the other: the line only
                // becomes available when both are in.
                Assert.That(source.TryGetTranslation("Are you ready, D'ark?", out _), Is.False);

                source.PlayerName = "D'ark One";
                Assert.That(source.TryGetTranslation("Are you ready, D'ark?", out _), Is.False,
                    "the gender is still unknown");

                source.PlayerIsFeminine = true;
                Assert.That(source.TryGetTranslation("Are you ready, D'ark?", out var feminine), Is.True);
                Assert.That(feminine, Is.EqualTo("D'ark, ты готова?"));

                // And it follows the character, not the first answer given.
                source.PlayerIsFeminine = false;
                Assert.That(source.TryGetTranslation("Are you ready, D'ark One?", out var masculine), Is.True);
                Assert.That(masculine, Is.EqualTo("D'ark One, ты готов?"));
            }
        }

        [Test]
        public void KnownLine_IsFound()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation("I am Hydaelyn. All made one.", out var translation), Is.True);
                Assert.That(translation, Is.EqualTo("Я — Хайделин. Множество в Одном."));
            }
        }

        // The game wraps dialogue where it likes and the reader hands it back
        // joined, so the stored line and the read one differ in whitespace alone.
        [Test]
        public void WrappedLine_IsFoundAllTheSame()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation("The wood...\n It's watching,   you know!  ", out var translation),
                    Is.True);
                Assert.That(translation, Is.EqualTo("Лес... Он бдит, знаешь ли!"));
            }
        }

        [Test]
        public void UnknownLine_IsNotAnswered()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation("Something nobody has written down", out var translation),
                    Is.False);
                Assert.That(translation, Is.Empty);
            }
        }

        // The game fills gender agreement in as it draws. A stored line that
        // still carries it has to be passed over: it reached the chat window
        // reading "когда ты с ним <var 08 E905 ((схлестнулась)) ((схлестнулся))
        // /var>", which is worse than paying a translator for the line.
        [Test]
        public void LineStillCarryingMarkup_IsPassedOver()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation("When you clashed with him", out var translation), Is.False);
                Assert.That(translation, Is.Empty);
            }
        }

        // Sound cues look like markup and are not: the game draws them as the
        // text they appear to be, so the line reads "<sigh> Here we go again."
        // on screen and has to keep them.
        [Test]
        public void SoundCue_IsNotMistakenForMarkup()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation("<sigh> Here we go again.", out var translation), Is.True);
                Assert.That(translation, Is.EqualTo("<sigh> Эх... Опять двадцать пять."));
            }
        }

        // Lines the game addresses to the player carry their name, and which
        // part of it varies by line. Trying the full name alone sent every one
        // of them to a translator, because what was on screen read "Go swiftly,
        // D'ark." while the name is "D'ark One".
        [TestCase("D'ark One", "The fate of Gridania hangs in the balance. Go swiftly, D'ark One.")]
        [TestCase("D'ark One", "The fate of Gridania hangs in the balance. Go swiftly, D'ark.")]
        [TestCase("D'ark One", "The fate of Gridania hangs in the balance. Go swiftly, One.")]
        public void LineAddressedToThePlayer_IsFoundByAnyFormOfTheName(string playerName, string spoken)
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                source.PlayerName = playerName;

                Assert.That(source.TryGetTranslation(spoken, out var translation), Is.True);
                Assert.That(translation, Does.StartWith("Судьба Гридании висит на волоске. Поторопись, "));
            }
        }

        [Test]
        public void ALineNamedOnlyInTheTranslation_IsFoundByItsPlainText()
        {
            // The screen shows a line with no name in it; the translation
            // addresses the player. Both lines the user first noticed missing
            // on a German client were this shape.
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                source.PlayerName = "D'ark One";

                Assert.That(source.TryGetTranslation("Und er wird deine Hilfe benötigen.", out var translation),
                    Is.True);

                // The forename, as the game addresses somebody - not "D'ark
                // One", and not whichever form happened to be tried last.
                Assert.That(translation, Is.EqualTo("Поторопись, D'ark."));
            }
        }

        [Test]
        public void ALineNamedOnlyInTheOriginal_IsFoundByEveryFormOfTheName()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                source.PlayerName = "D'ark One";

                Assert.Multiple(() =>
                {
                    Assert.That(source.TryGetTranslation("Well met, D'ark One.", out var full), Is.True);
                    Assert.That(full, Is.EqualTo("Приветствую."));

                    Assert.That(source.TryGetTranslation("Well met, D'ark.", out var forename), Is.True);
                    Assert.That(forename, Is.EqualTo("Приветствую."));
                });
            }
        }

        [Test]
        public void LineAddressedToThePlayer_IsNotFoundBeforeTheNameIsKnown()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation(
                        "The fate of Gridania hangs in the balance. Go swiftly, D'ark.", out _),
                    Is.False);
            }
        }

        // English rarely needs to know the player's gender - "adventurer" has
        // none - but Russian does, so these lines are kept both ways and chosen
        // between once the character is known.
        [TestCase(true, "Искательница приключений, на позицию.")]
        [TestCase(false, "Искатель приключений, на позицию.")]
        public void GenderedLine_IsPhrasedForTheCharacter(bool isFeminine, string expected)
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                source.PlayerIsFeminine = isFeminine;

                Assert.That(source.TryGetTranslation("This position is yours, adventurer.", out var translation),
                    Is.True);
                Assert.That(translation, Is.EqualTo(expected));
            }
        }

        // English can carry the agreement too - "this woman" against "this man"
        // - so the line reaching us differs by character, not just its
        // translation. Only the wording that character actually hears is kept.
        [TestCase(true, "Hydaelyn would speak to this woman...", "Хайделин говорила с этой женщиной...")]
        [TestCase(false, "Hydaelyn would speak to this man...", "Хайделин говорила с этим мужчиной...")]
        public void GenderedLine_IsFoundUnderTheEnglishThatCharacterHears(
            bool isFeminine, string spoken, string expected)
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                source.PlayerIsFeminine = isFeminine;

                Assert.That(source.TryGetTranslation(spoken, out var translation), Is.True);
                Assert.That(translation, Is.EqualTo(expected));
            }
        }

        [Test]
        public void GenderedLine_ForTheOtherCharacter_IsNotUsed()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                source.PlayerIsFeminine = false;

                Assert.That(source.TryGetTranslation("Hydaelyn would speak to this woman...", out _), Is.False);
            }
        }

        [Test]
        public void GenderedLine_IsLeftAloneUntilTheCharacterIsKnown()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetTranslation("This position is yours, adventurer.", out _), Is.False);
            }
        }

        [Test]
        public void SpeakerName_IsFound()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetSpeakerName("Mother Miounne", out var translated), Is.True);
                Assert.That(translated, Is.EqualTo("Матушка Миунна"));
            }
        }

        // The game writes a typographic apostrophe in some places and a plain
        // one in others for the same character.
        [TestCase("Y'shtola")]
        [TestCase("Y’shtola")]
        public void SpeakerName_IsFoundWhicheverApostropheIsUsed(string speaker)
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetSpeakerName(speaker, out var translated), Is.True);
                Assert.That(translated, Is.EqualTo("Я'штола"));
            }
        }

        // The game hides who is speaking behind "???" on purpose. The Russian
        // wrapper gave the game away - it named a stranger on a boat while the
        // English still read "???" - so a label with no letters in it never
        // reaches the index, and asking about one finds nothing.
        [Test]
        public void HiddenSpeaker_IsNotGivenAway()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetSpeakerName("???", out var translated), Is.False);
                Assert.That(translated, Is.Empty);
            }
        }

        [Test]
        public void SpeakerName_UnknownIsNotAnswered()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.TryGetSpeakerName("Somebody Nobody Named", out _), Is.False);
            }
        }

        [Test]
        public void LanguageOfTheIndex_IsReported()
        {
            using (var source = new SqliteReferenceTranslationSource(_databasePath, null))
            {
                Assert.That(source.LanguageCode, Is.EqualTo("ru"));
            }
        }

        // Not having the index is the ordinary state for anyone who has not built
        // it, and has to mean "translate everything" rather than a crash.
        [Test]
        public void MissingIndex_IsNotAnError()
        {
            using (var source = new SqliteReferenceTranslationSource(
                       Path.Combine(Path.GetTempPath(), "no-such-index.db"), null))
            {
                Assert.That(source.IsAvailable, Is.False);
                Assert.That(source.LanguageCode, Is.Empty);
                Assert.That(source.TryGetTranslation("Anything at all", out _), Is.False);
            }
        }

        [Test]
        public void Normalize_CollapsesWhitespaceTheWayTheIndexWasBuilt()
        {
            Assert.That(SqliteReferenceTranslationSource.Normalize("  a\r\n b\t\tc  "), Is.EqualTo("a b c"));
            Assert.That(SqliteReferenceTranslationSource.Normalize("   "), Is.Empty);
            Assert.That(SqliteReferenceTranslationSource.Normalize(null), Is.Empty);
        }
    }
}
