using System.Collections.Generic;
using System.Linq;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

using Translation.Models;

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
            Assert.That(settings.MessageContainerAlpha, Is.EqualTo(32));
            Assert.That(settings.MessageContainerBorderThickness, Is.EqualTo(0));
            Assert.That(settings.MessageContainerBorderAlpha, Is.EqualTo(96));
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
                MessageContainerAlpha = 80,
                MessageContainerBorderThickness = 2,
                MessageContainerBorderAlpha = 140,
                ShowOnlyLastMessage = true
            };

            var copy = new ChatWindowViewModelSettings(original);

            Assert.That(copy.ContentPadding, Is.EqualTo(21));
            Assert.That(copy.MessagesInContainer, Is.True);
            Assert.That(copy.MessageContainerPadding, Is.EqualTo(9));
            Assert.That(copy.MessageContainerAlpha, Is.EqualTo(80));
            Assert.That(copy.MessageContainerBorderThickness, Is.EqualTo(2));
            Assert.That(copy.MessageContainerBorderAlpha, Is.EqualTo(140));
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
                MessageContainerAlpha = 72,
                MessageContainerBorderThickness = 3,
                MessageContainerBorderAlpha = 128,
                ShowOnlyLastMessage = true
            };

            var languages = new List<TranslatorLanguage>
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
                    null,
                    allChatCodes,
                    hotKeyManager,
                    logger,
                    bindingService);

                var roundTripped = viewModel.GetSettings();

                Assert.That(roundTripped.ContentPadding, Is.EqualTo(18));
                Assert.That(roundTripped.MessagesInContainer, Is.True);
                Assert.That(roundTripped.MessageContainerPadding, Is.EqualTo(10));
                Assert.That(roundTripped.MessageContainerAlpha, Is.EqualTo(72));
                Assert.That(roundTripped.MessageContainerBorderThickness, Is.EqualTo(3));
                Assert.That(roundTripped.MessageContainerBorderAlpha, Is.EqualTo(128));
                Assert.That(roundTripped.ShowOnlyLastMessage, Is.True);
            }
            finally
            {
                hotKeyManager.Dispose();
            }
        }

        // The default chat codes used to be pinned here, as the values they
        // happened to have: 003D and 0044 off. That is now wrong on purpose,
        // and what replaces it says why - see ChatWindowDefaultChatCodesTests.

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