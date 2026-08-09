using NUnit.Framework;

using Translation.Reference;

namespace Translation.Tests.Reference
{
    // Each of these is a rule that was arrived at by handing the builder a line
    // that had come out machine-translated and finding why it had not matched.
    [TestFixture]
    public class ReferenceIndexBuilderTests
    {
        private static string Sheet(params (string Id, string Text)[] units)
        {
            var body = string.Empty;
            foreach (var unit in units)
            {
                body +=
                    $"<trans-unit id=\"{unit.Id}\"><source>{unit.Id}</source>" +
                    $"<target state=\"final\">{unit.Text}</target></trans-unit>";
            }

            return "<xliff><file><body>" + body + "</body></file></xliff>";
        }

        private static ReferenceIndexBuilder Build(string folder, string english, string translated)
        {
            var builder = new ReferenceIndexBuilder();
            builder.AddSheet(folder, english, translated);
            return builder;
        }

        [Test]
        public void ARowNobodyTranslated_DoesNotTakeTheNextRowsText()
        {
            // Half-translated sheets write the untouched rows as <target/>.
            // Reading across one of them put the next row's translation under
            // this row's number: 1 281 lines answered with a different line
            // altogether, and shown as somebody's hand-made work.
            var english =
                "<xliff><file><body>" +
                "<trans-unit id=\"59\"><source>59</source><target state=\"final\">Where has he gone?</target></trans-unit>" +
                "<trans-unit id=\"60\"><source>60</source><target state=\"final\">He yet lives!?</target></trans-unit>" +
                "</body></file></xliff>";

            var translated =
                "<xliff><file><body>" +
                "<trans-unit id=\"59\"><source>59</source><target/></trans-unit>" +
                "<trans-unit id=\"60\"><source>60</source><target state=\"translated\">Он всё ещё жив?!</target></trans-unit>" +
                "</body></file></xliff>";

            var builder = new ReferenceIndexBuilder();
            builder.AddSheet("exd/Quest/013", english, translated);

            Assert.Multiple(() =>
            {
                Assert.That(builder.Lines["He yet lives!?"], Is.EqualTo("Он всё ещё жив?!"));
                Assert.That(builder.Lines.ContainsKey("Where has he gone?"), Is.False,
                    "a row nobody translated must stay untranslated");
            });
        }

        [Test]
        public void PlainLine_IsIndexed()
        {
            // The export wraps a line with a real newline, and we read it
            // joined, so whitespace cannot be part of the key.
            var builder = Build("exd/Balloon",
                Sheet(("8", "The wood...\nIt's watching, you know!")),
                Sheet(("8", "Лес... Он бдит, знаешь ли!")));

            Assert.That(builder.Lines["The wood... It's watching, you know!"],
                Is.EqualTo("Лес... Он бдит, знаешь ли!"));
        }

        // Quest text carries its internal key ahead of the first tab.
        [Test]
        public void QuestKey_IsNotPartOfTheLine()
        {
            var builder = Build("exd/Quest/004/ManFst005",
                Sheet(("89", "TEXT_MANFST005_HYDAELYN&lt;tab&gt;I am Hydaelyn. All made one.")),
                Sheet(("89", "TEXT_MANFST005_HYDAELYN&lt;tab&gt;Я — Хайделин.")));

            Assert.That(builder.Lines.ContainsKey("I am Hydaelyn. All made one."), Is.True);
        }

        // Emphasis and the pair around a highlighted term only style what is
        // already there; the words are on screen as written.
        [Test]
        public void Formatting_IsTakenOutAndTheLineKept()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "The &lt;var 1A 02 /var&gt;ultimate&lt;var 1A 01 /var&gt; strategy!")),
                Sheet(("1", "Ультимативная стратегия!")));

            Assert.That(builder.Lines.ContainsKey("The ultimate strategy!"), Is.True);
        }

        // Sound cues look like markup and are drawn as the text they look like.
        [Test]
        public void SoundCue_StaysInTheLine()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "&lt;sigh&gt; Here we go again.")),
                Sheet(("1", "Эх... Опять двадцать пять.")));

            Assert.That(builder.Lines.ContainsKey("<sigh> Here we go again."), Is.True);
        }

        [Test]
        public void SubstitutedValue_MakesTheLineUnusable()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "You have &lt;var 20 E802 /var&gt; gil.")),
                Sheet(("1", "У вас &lt;var 20 E802 /var&gt; гил.")));

            Assert.That(builder.Lines, Is.Empty);
            Assert.That(builder.SkippedForMarkup, Is.EqualTo(1));
        }

        // Battle and cutscene lines carry the speaker inside the text. We read
        // the speaker from its own node, so the line alone has to match - and
        // the wrapper is where the speaker's translated name comes from.
        [Test]
        public void SpeakerWrapper_LeavesTheLineAndYieldsTheName()
        {
            var builder = Build("exd/NpcYell",
                Sheet(("1", "(-Ixali Occultists-)O mournful voice of creation!")),
                Sheet(("1", "(-Иксал-оккультист-)О, скорбный голос созидания!")));

            Assert.That(builder.Lines["O mournful voice of creation!"],
                Is.EqualTo("О, скорбный голос созидания!"));
            Assert.That(builder.Speakers["Ixali Occultists"], Is.EqualTo("Иксал-оккультист"));
        }

        // "???" is the game withholding an identity, and the Russian wrapper
        // gives it away. Translating it spoils the scene it was hiding.
        [Test]
        public void HiddenSpeaker_IsNotRecorded()
        {
            var builder = Build("exd/NpcYell",
                Sheet(("1", "(-???-)Shut your gobs and turn around.")),
                Sheet(("1", "(-Дружелюбный пассажир-)Всё, харэ.")));

            Assert.That(builder.Speakers, Is.Empty);
        }

        // The roster stores "Name<tab>Title"; only the name is ever spoken.
        [Test]
        public void Roster_YieldsOnlyTheName()
        {
            var builder = Build("exd/ENpcResident",
                Sheet(("1", "Mother Miounne&lt;tab&gt;Carline Canopy Proprietress")),
                Sheet(("1", "Матушка Миунна&lt;tab&gt;Хозяйка „Цветочного навеса“")));

            Assert.That(builder.Speakers["Mother Miounne"], Is.EqualTo("Матушка Миунна"));
        }

        // Languages address the player in different places. Requiring that the
        // two sides agreed about it threw away whole conversations: 2 657 lines
        // for a German client, 3 697 for a Japanese one, and 576 even for
        // English. Whichever side carries the name, the other is a fixed
        // string, so the line can still be pinned down.
        [Test]
        public void NamedOnlyInTheOriginal_IsStillAPattern()
        {
            // The game says the name, the translators did not: the key needs
            // the name written in, the translation is a fixed string.
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Go swiftly, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")),
                Sheet(("1", "Поторопись.")));

            Assert.That(builder.Patterns["Go swiftly, " + ReferenceIndexBuilder.PlayerPlaceholder + "."],
                Is.EqualTo("Поторопись."));
        }

        [Test]
        public void NamedOnlyInTheTranslation_IsStillAPattern()
        {
            // The German for this row does not name the player; the Russian
            // does. Both of the lines the user first noticed missing were this.
            var builder = Build("exd/Quest/004",
                Sheet(("1", "Und er wird deine Hilfe benötigen. Viel Glück.")),
                Sheet(("1", "Судьба Гридании висит на волоске. Поторопись, " +
                            "&lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")));

            Assert.That(builder.Patterns["Und er wird deine Hilfe benötigen. Viel Glück."],
                Is.EqualTo("Судьба Гридании висит на волоске. Поторопись, " +
                           ReferenceIndexBuilder.PlayerPlaceholder + "."));
        }

        [Test]
        public void TwoNamesInOneLine_IsStillNotAPattern()
        {
            // Two placeholders and the pieces between them stop pinning the
            // line down, whichever side they are on.
            const string name = "&lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;";
            var builder = Build("exd/Quest/001",
                Sheet(("1", name + ", listen. " + name + ", please.")),
                Sheet(("1", "Послушай.")));

            Assert.That(builder.Patterns, Is.Empty);
            Assert.That(builder.Lines, Is.Empty);
        }

        // German is hyphenated and spaced by the game, and both were being read
        // as substitutions - which threw the line away. What the screen gives
        // us settles what they are: the raw log shows "Kristallturms" whole and
        // "in der Zukunft ..." with a space, so one is nothing and one is a
        // space.
        [Test]
        public void APlaceToBreakALongWord_IsNotPartOfTheWord()
        {
            var builder = Build("exd/cut_scene/050",
                Sheet(("1", "Deshalb machte ich mich zu einem Teil des Kris&lt;var 16 /var&gt;tallturms.")),
                Sheet(("1", "Поэтому я стал частью Кристальной башни.")));

            Assert.That(builder.Lines["Deshalb machte ich mich zu einem Teil des Kristallturms."],
                Is.EqualTo("Поэтому я стал частью Кристальной башни."));
        }

        [Test]
        public void ASpaceTheGameWillNotBreakAt_IsStillASpace()
        {
            // German sets the ellipsis off with one, and writes "10 %" the same
            // way. It reaches the screen as a space, so it is one here.
            var builder = Build("exd/cut_scene/050",
                Sheet(("1", "Mein Schicksal erwartet mich in der Zukunft&lt;var 1D /var&gt;...")),
                Sheet(("1", "Будущее, где притаилась моя судьба...")));

            Assert.That(builder.Lines["Mein Schicksal erwartet mich in der Zukunft ..."],
                Is.EqualTo("Будущее, где притаилась моя судьба..."));
        }

        [Test]
        public void ALineWithBothAGenderAndAName_IsKeptForEachGender()
        {
            // These reached neither store: the gender branch gave up on the
            // name, and the pattern branch gave up on the gender. They are the
            // game's most personal lines - addressed to you and worded for you.
            const string name = "&lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;";
            const string gender = "&lt;var 08 E905 ((готова)) ((готов)) /var&gt;";

            var builder = Build("exd/Quest/001",
                Sheet(("1", "Are you ready, " + name + "?")),
                Sheet(("1", name + ", ты " + gender + "?")));

            var key = "Are you ready, " + ReferenceIndexBuilder.PlayerPlaceholder + "?";

            Assert.Multiple(() =>
            {
                Assert.That(builder.GenderedPatterns[(key, true)],
                    Is.EqualTo(ReferenceIndexBuilder.PlayerPlaceholder + ", ты готова?"));
                Assert.That(builder.GenderedPatterns[(key, false)],
                    Is.EqualTo(ReferenceIndexBuilder.PlayerPlaceholder + ", ты готов?"));

                // And not among the plain gendered lines, where the name would
                // still be a hole nothing on screen can match.
                Assert.That(builder.Gendered, Is.Empty);
            });
        }

        [Test]
        public void PlayerName_BecomesAPattern()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Go swiftly, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")),
                Sheet(("1", "Поторопись, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")));

            Assert.That(builder.Patterns["Go swiftly, " + ReferenceIndexBuilder.PlayerPlaceholder + "."],
                Is.EqualTo("Поторопись, " + ReferenceIndexBuilder.PlayerPlaceholder + "."));
        }

        // The name arrives bare more often than wrapped - 14,515 lines against
        // 14,086 in the export of 5 August - and only the wrapped form was
        // known. Every bare one was thrown away as markup nothing could stand
        // in for, which is how a Serpent Personnel Officer came to ask for an
        // oath of allegiance in machine Russian.
        [Test]
        public void PlayerName_IsAlsoRecognisedWithoutItsWrapper()
        {
            const string name = "&lt;var 29 EB02 /var&gt;";

            // Both sides exactly as exd/Quest/006/ManFst303_00683 has them.
            var builder = Build("exd/Quest/006",
                Sheet(("55", $"TEXT_MANFST303_00683_SERPENTPERSONNEL_000_8&lt;tab&gt;Now then, {name}, " +
                             "I ask that you give unto us your oath of allegiance, in whatever fashion you see fit.")),
                Sheet(("55", $"TEXT_MANFST303_00683_SERPENTPERSONNEL_000_8&lt;tab&gt;А теперь, {name}, " +
                             "прошу тебя принести присягу верности — в вольной форме.")));

            var placeholder = ReferenceIndexBuilder.PlayerPlaceholder;

            Assert.That(
                builder.Patterns["Now then, " + placeholder + ", I ask that you give unto us your oath of " +
                                 "allegiance, in whatever fashion you see fit."],
                Is.EqualTo("А теперь, " + placeholder + ", прошу тебя принести присягу верности — в вольной форме."));
        }

        [Test]
        public void TheWrappedName_IsStillTakenWhole()
        {
            // The wrapper contains the bare tag. Matching the inner one first
            // would punch out the name and leave "<var 2C (()) (( )) 02
            // /var>" behind - still markup, so the line would still be thrown
            // away, and the fix above would have quietly broken the case that
            // already worked.
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Go swiftly, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")),
                Sheet(("1", "Поторопись, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")));

            var placeholder = ReferenceIndexBuilder.PlayerPlaceholder;

            Assert.That(builder.Patterns["Go swiftly, " + placeholder + "."],
                Is.EqualTo("Поторопись, " + placeholder + "."));
        }

        [Test]
        public void TheRestOfTheVar29Family_IsNotTheName()
        {
            // EB02 is the player. EA01 upwards are other things the game fills
            // in, and writing somebody's name over a number would be worse than
            // dropping the line - a wrong translation reads as a real one.
            var builder = Build("exd/Quest/001",
                Sheet(("1", "You have &lt;var 29 EA02 /var&gt; gil remaining.")),
                Sheet(("1", "У вас осталось &lt;var 29 EA02 /var&gt; гилей.")));

            Assert.That(builder.Patterns, Is.Empty);
            Assert.That(builder.Lines, Is.Empty);
        }

        // The game writes the hyphen in a name it will not break across lines
        // as its own tag, and that tag was being deleted along with the colour
        // and emphasis it sat beside. The index then held "KanESenna" against a
        // screen that says "Kan-E-Senna" - and with it Radz-at-Han, Toto-Rak,
        // Mun-Tuy, city-state, ill-fated and Hatching-tide.
        [Test]
        public void AHyphenTheGameWillNotBreak_IsAHyphen()
        {
            const string hyphen = "&lt;var 1F /var&gt;";

            var builder = Build("exd/Quest/006",
                Sheet(("52", $"TEXT_MANFST303_00683_SERPENTPERSONNEL_000_5&lt;tab&gt;Elder Seedseer Kan{hyphen}E" +
                             $"{hyphen}Senna is the supreme commander of our forces.")),
                Sheet(("52", "TEXT_MANFST303_00683_SERPENTPERSONNEL_000_5&lt;tab&gt;Верховная Жрица " +
                             "Кан-Э-Сенна — верховный главнокомандующий наших сил.")));

            Assert.That(
                builder.Lines["Elder Seedseer Kan-E-Senna is the supreme commander of our forces."],
                Is.EqualTo("Верховная Жрица Кан-Э-Сенна — верховный главнокомандующий наших сил."));
        }

        [Test]
        public void APlaceTheGameMayBreakAWord_IsStillNothing()
        {
            // <var 16> and <var 1F> look alike and are opposites: one is drawn
            // only if the line breaks there, the other is always drawn. Reading
            // the first as a hyphen would put one in the middle of every long
            // German word.
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Waf&lt;var 16 /var&gt;fen&lt;var 16 /var&gt;fertigkeiten")),
                Sheet(("1", "Владение оружием")));

            Assert.That(builder.Lines["Waffenfertigkeiten"], Is.EqualTo("Владение оружием"));
        }

        // The translation tool leaves its row number at the front of the text
        // it has not had a translation typed into yet. Kept, it makes a line
        // that differs from the English and so looks like somebody's work.
        [Test]
        public void ARowNumberLeftInFrontOfTheEnglish_IsNotATranslation()
        {
            var builder = Build("exd/Addon",
                Sheet(("1", "Cancel registration.")),
                Sheet(("1", "9547_Cancel registration.")));

            // Nothing to show: with the number gone it is the English again,
            // and a line that translates to itself is no translation.
            Assert.That(builder.Lines, Is.Empty);
        }

        [Test]
        public void ARealTranslationTypedAfterTheRowNumber_IsKept()
        {
            // 72 rows in the export look like this. Dropping every numbered
            // row would have thrown them away; taking the number off is what
            // makes them findable.
            var builder = Build("exd/Addon",
                Sheet(("1", "Fire")),
                Sheet(("1", "243_Огонь")));

            Assert.That(builder.Lines["Fire"], Is.EqualTo("Огонь"));
        }

        [Test]
        public void ANumberThatIsPartOfTheLine_IsLeftAlone()
        {
            // The prefix is digits then an underscore. A line that merely
            // starts with a number is a line.
            var builder = Build("exd/Quest/001",
                Sheet(("1", "10 gil for the trouble.")),
                Sheet(("1", "10 гилей за беспокойство.")));

            Assert.That(builder.Lines["10 gil for the trouble."],
                Is.EqualTo("10 гилей за беспокойство."));
        }

        // English can carry the agreement as readily as Russian, so the line
        // reaching us differs by character and is stored under each.
        [Test]
        public void GenderAgreement_IsKeptBothWays()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Hydaelyn would speak to this &lt;var 08 E905 ((woman)) ((man)) /var&gt;...")),
                Sheet(("1", "Хайделин говорила с &lt;var 08 E905 ((этой женщиной)) ((этим мужчиной)) /var&gt;...")));

            Assert.That(builder.Gendered[("Hydaelyn would speak to this woman...", true)],
                Is.EqualTo("Хайделин говорила с этой женщиной..."));
            Assert.That(builder.Gendered[("Hydaelyn would speak to this man...", false)],
                Is.EqualTo("Хайделин говорила с этим мужчиной..."));
        }

        // A condition we cannot answer - this one asks about the controller -
        // has to leave the line to a translator rather than be guessed at.
        [Test]
        public void OtherConditions_AreNotResolved()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Stick Sensitivity")),
                Sheet(("1", "&lt;var 08 E4EB02EB03 ((Джойстик)) ((Мини-джойстик)) /var&gt;")));

            Assert.That(builder.Gendered, Is.Empty);
            Assert.That(builder.Lines, Is.Empty);
        }

        [Test]
        public void UntranslatedLine_IsNotIndexed()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "May you find favor with the elementals.")),
                Sheet(("1", "May you find favor with the elementals.")));

            Assert.That(builder.Lines, Is.Empty);
        }
    }
}
