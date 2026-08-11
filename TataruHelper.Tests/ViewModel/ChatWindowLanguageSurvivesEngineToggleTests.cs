using System.Collections.Generic;
using System.Collections.Specialized;
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
    /// <summary>
    /// Switching any engine off in settings used to move the reading language
    /// off whatever the user had chosen.
    ///
    /// The engine picker binds its selection two-way, so when the engine list is
    /// rebuilt the picker writes a null selection back on the way through. A null
    /// engine tears both language lists down - they are built from the engine's
    /// supported languages - so the rebuilt lists have no memory of the choice
    /// and settle on the first language they contain. The engine being switched
    /// off did not have to be the selected one.
    /// </summary>
    [TestFixture]
    public class ChatWindowLanguageSurvivesEngineToggleTests
    {
        private static readonly List<TranslatorLanguage> Languages = new()
        {
            new TranslatorLanguage("Auto", "Auto", "auto"),
            new TranslatorLanguage("English", "English", "en"),
            new TranslatorLanguage("Russian", "Russian", "ru")
        };

        [Test]
        public void SwitchingAnotherEngineOff_KeepsTheChosenReadingLanguage()
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
                MimicThePickersTwoWayBinding(viewModel);

                var russian = Languages.Single(x => x.SystemName == "Russian");
                viewModel.TranslateToLanguages.MoveCurrentTo(russian);
                Assume.That(viewModel.TranslateToLanguages.CurrentItem, Is.EqualTo(russian));

                // Any engine at all - not the selected one.
                availability.IsPapagoEnabled = true;

                Assert.That(viewModel.TranslateToLanguages.CurrentItem, Is.EqualTo(russian),
                    "the reading language moved when an unrelated engine was switched");
                Assert.That(viewModel.SelectedEngine, Is.Not.Null);
                Assert.That(viewModel.SelectedEngine.EngineName, Is.EqualTo(TranslationEngineName.DeepL));
            });
        }

        /// <summary>
        /// A ComboBox drops its selection when its items are cleared and, being
        /// bound two-way, writes that null back. Nothing else here stands in for
        /// the view, so the collection is watched directly - this is the part of
        /// the view that the bug depends on.
        /// </summary>
        private static void MimicThePickersTwoWayBinding(ChatWindowViewModel viewModel)
        {
            viewModel.AvailableEngines.CollectionChanged += (_, e) =>
            {
                if (e.Action == NotifyCollectionChangedAction.Reset)
                    viewModel.SelectedEngine = null;
            };
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
                assertions(new ChatWindowViewModel(
                    settings, translationEngines, availability, allChatCodes,
                    hotKeyManager, logger, bindingService));
            }
            finally
            {
                hotKeyManager.Dispose();
            }
        }

        private sealed class NullLogger : IAppLogger
        {
            public void WriteLog(string input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteLog(object input, string memberName = "", int sourceLineNumber = 0) { }
            public void WriteConsoleLog(string input) { }
            public void WriteChatLog(string input) { }
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
            public string GetEndpoint(TranslationEngineName engine) => string.Empty;
            public void SetApiKey(TranslationEngineName engine, string apiKey) { }
            public void SetRegion(TranslationEngineName engine, string region) { }
            public void SetModel(TranslationEngineName engine, string model) { }
            public void SetEndpoint(TranslationEngineName engine, string endpoint) { }
            public void Save() { }
        }
    }
}
