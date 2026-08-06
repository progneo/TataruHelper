using FFXIVTataruHelper;

using NUnit.Framework;

namespace TataruHelper.Tests.Models
{
    // The switch that replaced the python builder. It is how a release gets its
    // index, so getting the arguments wrong means shipping the wrong file -
    // or, worse, an application that starts its interface when a build script
    // expected it to build and exit.
    [TestFixture]
    public class ReferenceIndexCommandTests
    {
        [Test]
        public void AnOrdinaryLaunch_IsNotACommand()
        {
            Assert.That(ReferenceIndexCommand.Parse(new string[0]), Is.Null);
            Assert.That(ReferenceIndexCommand.Parse(new[] { "--log-raw-dialog" }), Is.Null);
            Assert.That(ReferenceIndexCommand.Parse(null), Is.Null);
        }

        [Test]
        public void TheOtherSwitches_MeanNothingOnTheirOwn()
        {
            // Otherwise a stray "--output" from somewhere else would stop the
            // application from starting.
            Assert.That(ReferenceIndexCommand.Parse(new[] { "--output", "x.db" }), Is.Null);
        }

        [Test]
        public void AskedPlainly_BuildsFromGitHubWhereTheApplicationReads()
        {
            var command = ReferenceIndexCommand.Parse(new[] { "--build-reference-index" });

            Assert.That(command, Is.Not.Null);
            Assert.That(command.BuildsFromFolder, Is.False);
            Assert.That(command.Language, Is.Empty, "left to the settings");
            Assert.That(command.OutputPath, Is.Empty, "left to the settings");
        }

        [Test]
        public void AFolder_IsReadInsteadOfDownloading()
        {
            var command = ReferenceIndexCommand.Parse(new[]
            {
                "--build-reference-index", "--source", @"e:\xiv_ru_weblate-main", "--language", "ru",
                "--output", @"e:\out\index.db"
            });

            Assert.That(command.BuildsFromFolder, Is.True);
            Assert.That(command.SourceFolder, Is.EqualTo(@"e:\xiv_ru_weblate-main"));
            Assert.That(command.Language, Is.EqualTo("ru"));
            Assert.That(command.OutputPath, Is.EqualTo(@"e:\out\index.db"));
        }

        [Test]
        public void GitHub_IsSpeltOutRatherThanTakenForAFolder()
        {
            // The python builder took --source github to mean the project, and
            // a script carried over verbatim would otherwise look for a folder
            // called "github".
            var command = ReferenceIndexCommand.Parse(new[] { "--build-reference-index", "--source", "github" });

            Assert.That(command.BuildsFromFolder, Is.False);
        }

        [Test]
        public void AMissingValue_DoesNotSwallowTheNextSwitch()
        {
            var command = ReferenceIndexCommand.Parse(new[]
            {
                "--build-reference-index", "--language", "--output", "x.db"
            });

            Assert.That(command.Language, Is.Empty);
            Assert.That(command.OutputPath, Is.EqualTo("x.db"));
        }

        [Test]
        public void TheSwitchIsSpeltHoweverItIsTyped()
        {
            // The application already accepts -prerelease and --log-raw-dialog,
            // so both leaders and either case have to work here too.
            Assert.That(ReferenceIndexCommand.Parse(new[] { "-build-reference-index" }), Is.Not.Null);
            Assert.That(ReferenceIndexCommand.Parse(new[] { "/Build-Reference-Index" }), Is.Not.Null);
        }
    }
}
