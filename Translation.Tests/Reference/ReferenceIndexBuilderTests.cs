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

        [Test]
        public void PlayerName_BecomesAPattern()
        {
            var builder = Build("exd/Quest/001",
                Sheet(("1", "Go swiftly, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")),
                Sheet(("1", "Поторопись, &lt;var 2C ((&lt;var 29 EB02 /var&gt;)) (( )) 02 /var&gt;.")));

            Assert.That(builder.Patterns["Go swiftly, " + ReferenceIndexBuilder.PlayerPlaceholder + "."],
                Is.EqualTo("Поторопись, " + ReferenceIndexBuilder.PlayerPlaceholder + "."));
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
