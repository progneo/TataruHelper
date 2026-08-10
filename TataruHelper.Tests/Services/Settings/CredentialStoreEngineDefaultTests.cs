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
    }
}
