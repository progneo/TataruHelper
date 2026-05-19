using System.Collections.Generic;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

using Translation;

using Color = System.Windows.Media.Color;

namespace TataruHelper.Tests
{
    public class ChatWindowLayoutSettingsTests
    {
        [Test]
        public void Defaults_IncludeNewLayoutSettings()
        {
            var settings = new ChatWindowViewModelSettings();

            Assert.That(settings.ContentPadding, Is.EqualTo(12));
            Assert.That(settings.MessagesInContainer, Is.False);
            Assert.That(settings.MessageContainerPadding, Is.EqualTo(6));
            Assert.That(settings.ShowOnlyLastMessage, Is.False);
        }

        [Test]
        public void CopyConstructor_PreservesNewLayoutSettings()
        {
            var original = new ChatWindowViewModelSettings
            {
                ContentPadding = 21,
                MessagesInContainer = true,
                MessageContainerPadding = 9,
                ShowOnlyLastMessage = true
            };

            var copy = new ChatWindowViewModelSettings(original);

            Assert.That(copy.ContentPadding, Is.EqualTo(21));
            Assert.That(copy.MessagesInContainer, Is.True);
            Assert.That(copy.MessageContainerPadding, Is.EqualTo(9));
            Assert.That(copy.ShowOnlyLastMessage, Is.True);
        }

        [Test]
        public void GetSettings_RoundTripsNewLayoutSettings()
        {
            var settings = new ChatWindowViewModelSettings("1", 0)
            {
                ContentPadding = 18,
                MessagesInContainer = true,
                MessageContainerPadding = 10,
                ShowOnlyLastMessage = true
            };

            var languages = new List<TranslatorLanguague>
            {
                new("Auto", "Auto", "auto"), new("English", "English", "en")
            };
            settings.FromLanguague = languages[0];
            settings.ToLanguague = languages[1];

            var translationEngines = new List<TranslationEngine>
            {
                new(TranslationEngineName.GoogleTranslate, languages, 1.0)
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
                    allChatCodes,
                    hotKeyManager,
                    logger,
                    bindingService);

                var roundTripped = viewModel.GetSettings();

                Assert.That(roundTripped.ContentPadding, Is.EqualTo(18));
                Assert.That(roundTripped.MessagesInContainer, Is.True);
                Assert.That(roundTripped.MessageContainerPadding, Is.EqualTo(10));
                Assert.That(roundTripped.ShowOnlyLastMessage, Is.True);
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
    }
}