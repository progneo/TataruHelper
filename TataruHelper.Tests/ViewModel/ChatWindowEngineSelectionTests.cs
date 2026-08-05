using System.Collections.Generic;
using System.Linq;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

using Translation.Credentials;
using Translation.Models;

using Color = System.Windows.Media.Color;

namespace TataruHelper.Tests.ViewModel
{
    // The window is built before the credential store has answered which
    // engines are enabled, so the engine list starts empty and nothing can be
    // selected. Whatever recovers when the engines do arrive has to cope with
    // there being no selection at all - otherwise the engine and both language
    // pickers stay blank until the application is restarted.
    [TestFixture]
    public class ChatWindowEngineSelectionTests
    {
        private static readonly List<TranslatorLanguage> Languages = new()
        {
            new TranslatorLanguage("Auto", "Auto", "auto"),
            new TranslatorLanguage("English", "English", "en"),
            new TranslatorLanguage("Russian", "Russian", "ru")
        };

        [Test]
        public void EnginesArrivingAfterConstruction_SelectTheSavedEngine()
        {
            var store = new FakeCredentialStore { EverythingEnabled = false };
            var availability = new TranslationCredentialsViewModel(store);
            Assume.That(availability.AvailableEngines, Is.Empty);

            var settings = new ChatWindowViewModelSettings("1", 0)
            {
                TranslationEngineName = TranslationEngineName.DeepL,
                FromLanguague = Languages[0],
                ToLanguague = Languages[2]
            };

            RunWithViewModel(settings, availability, viewModel =>
            {
                Assert.That(viewModel.SelectedEngine, Is.Null, "nothing is available yet");

                // The store answers at last.
                availability.IsDeepLEnabled = true;

                Assert.That(viewModel.SelectedEngine, Is.Not.Null,
                    "the engines arrived, so one of them has to be selected");
                Assert.That(viewModel.SelectedEngine.EngineName, Is.EqualTo(TranslationEngineName.DeepL),
                    "the engine saved in the settings is the one to restore");
                Assert.That(viewModel.TranslateFromLanguages.CurrentItem, Is.Not.Null);
                Assert.That(viewModel.TranslateToLanguages.CurrentItem, Is.Not.Null);
            });
        }

        [Test]
        public void EnginesArrivingAfterConstruction_FallBackWhenTheSavedEngineIsDisabled()
        {
            var store = new FakeCredentialStore { EverythingEnabled = false };
            var availability = new TranslationCredentialsViewModel(store);

            var settings = new ChatWindowViewModelSettings("1", 0)
            {
                TranslationEngineName = TranslationEngineName.Papago
            };

            RunWithViewModel(settings, availability, viewModel =>
            {
                availability.IsGoogleTranslateEnabled = true;

                Assert.That(viewModel.SelectedEngine, Is.Not.Null);
                Assert.That(viewModel.SelectedEngine.EngineName,
                    Is.EqualTo(TranslationEngineName.GoogleTranslate));
            });
        }

        [Test]
        public void SelectedEngineBeingDisabled_StillFallsBackToWhatIsLeft()
        {
            var store = new FakeCredentialStore { EverythingEnabled = false };
            store.Enabled.Add(TranslationEngineName.GoogleTranslate);
            store.Enabled.Add(TranslationEngineName.DeepL);
            var availability = new TranslationCredentialsViewModel(store);

            var settings = new ChatWindowViewModelSettings("1", 0)
            {
                TranslationEngineName = TranslationEngineName.DeepL
            };

            RunWithViewModel(settings, availability, viewModel =>
            {
                Assume.That(viewModel.SelectedEngine.EngineName, Is.EqualTo(TranslationEngineName.DeepL));

                // Straight at the store: turning the selected engine off through
                // the view model is refused while it is the last one enabled.
                store.Enabled.Remove(TranslationEngineName.DeepL);
                availability.IsPapagoEnabled = true;

                Assert.That(viewModel.SelectedEngine.EngineName,
                    Is.EqualTo(TranslationEngineName.GoogleTranslate));
            });
        }

        private static void RunWithViewModel(
            ChatWindowViewModelSettings settings,
            TranslationCredentialsViewModel availability,
            System.Action<ChatWindowViewModel> assertions)
        {
            var translationEngines = new List<TranslationEngine>
            {
                new(TranslationEngineName.GoogleTranslate, Languages, 1.0),
                new(TranslationEngineName.DeepL, Languages, 2.0)
            };

            var allChatCodes = new List<ChatMsgType>
            {
                new("0039", MsgType.Translate, "System", Color.FromArgb(255, 255, 255, 255))
            };

            var logger = new NullLogger();
            var hotKeyManager = new HotKeyManager(null);
            var bindingService = new HotKeyBindingService(logger);

            try
            {
                var viewModel = new ChatWindowViewModel(
                    settings,
                    translationEngines,
                    availability,
                    allChatCodes,
                    hotKeyManager,
                    logger,
                    bindingService);

                assertions(viewModel);
            }
            finally
            {
                hotKeyManager.Dispose();
            }
        }

        private sealed class NullLogger : IAppLogger
        {
            public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0)
            {
            }

            public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0)
            {
            }

            public void WriteConsoleLog(string input)
            {
            }

            public void WriteChatLog(string input)
            {
            }
        }

        private sealed class FakeCredentialStore : ITranslationCredentialStore
        {
            public bool EverythingEnabled { get; set; } = true;

            public HashSet<TranslationEngineName> Enabled { get; } = new();

            public bool IsEngineEnabled(TranslationEngineName engine)
                => EverythingEnabled || Enabled.Contains(engine);

            public void SetEngineEnabled(TranslationEngineName engine, bool isEnabled)
            {
                if (isEnabled) Enabled.Add(engine);
                else Enabled.Remove(engine);
            }

            public string GetApiKey(TranslationEngineName engine) => string.Empty;

            public string GetRegion(TranslationEngineName engine) => string.Empty;

            public string GetModel(TranslationEngineName engine) => string.Empty;

            public void SetApiKey(TranslationEngineName engine, string apiKey)
            {
            }

            public void SetRegion(TranslationEngineName engine, string region)
            {
            }

            public void SetModel(TranslationEngineName engine, string model)
            {
            }

            public void Save()
            {
            }
        }
    }
}
