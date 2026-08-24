using FFXIVTataruHelper.Services.Settings;

using NUnit.Framework;

using System;
using System.IO;

using Translation.Models;

namespace TataruHelper.Tests
{
    /// <summary>
    /// An engine that needs a server the user runs themselves must not be
    /// offered until they say so: on by default it would sit in the picker
    /// pointing at nothing, and be knocked on as a stand-in on every failed
    /// line. Everything else stays on, so no existing install loses an engine.
    /// </summary>
    public class CredentialStoreEngineDefaultTests
    {
        private static DpapiCredentialStore NewStore()
        {
            var directory = Path.Combine(
                Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return new DpapiCredentialStore(directory);
        }

        [Test]
        public void UntouchedStore_LeavesServerBackedEnginesOff()
        {
            var sut = NewStore();

            Assert.That(sut.IsEngineEnabled(TranslationEngineName.Ollama), Is.False);
            Assert.That(sut.IsEngineEnabled(TranslationEngineName.LmStudio), Is.False);
        }

        [Test]
        public void UntouchedStore_LeavesEveryOtherEngineOn()
        {
            var sut = NewStore();

            Assert.That(sut.IsEngineEnabled(TranslationEngineName.GoogleTranslate), Is.True);
            Assert.That(sut.IsEngineEnabled(TranslationEngineName.OpenAI), Is.True);
            Assert.That(sut.IsEngineEnabled(TranslationEngineName.OpenRouter), Is.True);
        }

        /// <summary>
        /// Enabling used to be stored as an empty string, and the store forgets
        /// empty values. For an engine that is off by default that reads back as
        /// off - the switch would flip and undo itself.
        /// </summary>
        [Test]
        public void TurningOnAnEngineThatIsOffByDefault_Holds()
        {
            var sut = NewStore();

            sut.SetEngineEnabled(TranslationEngineName.Ollama, true);

            Assert.That(sut.IsEngineEnabled(TranslationEngineName.Ollama), Is.True);
        }

        [Test]
        public void TurningOffAnEngineThatIsOnByDefault_Holds()
        {
            var sut = NewStore();

            sut.SetEngineEnabled(TranslationEngineName.GoogleTranslate, false);

            Assert.That(sut.IsEngineEnabled(TranslationEngineName.GoogleTranslate), Is.False);
        }

        [Test]
        public void ChoicesSurviveReopening()
        {
            var directory = Path.Combine(
                Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            var first = new DpapiCredentialStore(directory);
            first.SetEngineEnabled(TranslationEngineName.Ollama, true);
            first.SetEngineEnabled(TranslationEngineName.GoogleTranslate, false);
            first.Save();

            var second = new DpapiCredentialStore(directory);

            Assert.That(second.IsEngineEnabled(TranslationEngineName.Ollama), Is.True);
            Assert.That(second.IsEngineEnabled(TranslationEngineName.GoogleTranslate), Is.False);
        }

        /// <summary>
        /// When the file on disk will not read (DPAPI refusing, a crash that
        /// left it torn), a save must not write the in-memory state - nothing,
        /// since the load came back empty - over it. The keys are already lost
        /// to this session; clobbering the file makes that permanent.
        /// </summary>
        [Test]
        public void AFileThatWillNotRead_IsNotOverwrittenByASave()
        {
            var directory = Path.Combine(
                Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            var secretsPath = Path.Combine(directory, "Secrets.dat");
            var original = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            File.WriteAllBytes(secretsPath, original);

            var sut = new DpapiCredentialStore(directory);
            sut.SetApiKey(TranslationEngineName.OpenAI, "a key that must not clobber the file");
            sut.Save();

            Assert.That(File.ReadAllBytes(secretsPath), Is.EqualTo(original));
        }
    }
}
