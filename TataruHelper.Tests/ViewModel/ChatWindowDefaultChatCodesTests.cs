using System;
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
    // Which codes a fresh window starts listening to decides whether the
    // application does anything at all for somebody who has just installed it.
    //
    // 003D and 0044 used to start unticked, to stop each NPC line appearing
    // twice - once live from the Talk addon, once when the player clicked
    // through. That is no longer what prevents the double: while realtime
    // reading is on, FFMemoryReader.IsCoveredByRealtimeReading drops both codes
    // before they reach any window. All the untick still decided was what
    // happens with realtime reading off, and the answer was nothing at all.
    [TestFixture]
    public class ChatWindowDefaultChatCodesTests
    {
        private static readonly List<TranslatorLanguage> Languages = new()
        {
            new TranslatorLanguage("English", "English", "en"),
            new TranslatorLanguage("Russian", "Russian", "ru")
        };

        /// <summary>The codes as the shipped ChatCodes.json describes them.</summary>
        private static List<ChatMsgType> ShippedCodes()
        {
            var white = Color.FromArgb(255, 255, 255, 255);

            return new List<ChatMsgType>
            {
                new("0039", MsgType.Translate, "System", white),
                new("003D", MsgType.Translate, "NPCD", white),
                new("0044", MsgType.Translate, "NPCA", white),
                new("2AB9", MsgType.Translate, "BossQuotes", white),
                new("000A", MsgType.Skip, "Say", white),
                new("000E", MsgType.Skip, "Party", white),
                new("001B", MsgType.Skip, "NoviceNetwork", white)
            };
        }

        [Test]
        public void AFreshWindow_ListensToDialogueWhicheverWayItArrives()
        {
            // One tick per channel, covering both the line read off the screen
            // and the chat log's later copy of it. Which of the two a player
            // gets is what the Real-Time Translation switch decides.
            WithFreshWindow(codes =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsTicked(codes, "003D"), Is.True, "NPC dialogue");
                    Assert.That(IsTicked(codes, "0044"), Is.True, "cutscene dialogue");
                });
            });
        }

        // A window saved before the two ways a line can arrive were merged holds
        // ticks for four codes, and which pair it kept ticked depended on the
        // version it was made under: 1.0.0 ticked the live pair and left the
        // chat-log pair alone. Read literally, such a window looks like one with
        // dialogue switched off, and its owner would be left with silence.
        [Test]
        public void AWindowFromBeforeTheMerge_KeepsItsDialogue()
        {
            var settings = NewWindowSettings();
            settings.ChatCodes = new List<ChatCodeViewModel>
            {
                new("003D", "NPCD", Color.FromArgb(255, 1, 2, 3), false),
                new("0044", "NPCA", Color.FromArgb(255, 1, 2, 3), false),
                new("F03D", "NPCDRealtime", Color.FromArgb(255, 1, 2, 3), true),
                new("F044", "NPCARealtime", Color.FromArgb(255, 1, 2, 3), true)
            };

            WithWindow(settings, codes =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsTicked(codes, "003D"), Is.True, "was ticked as F03D");
                    Assert.That(IsTicked(codes, "0044"), Is.True, "was ticked as F044");
                });
            });
        }

        [Test]
        public void AWindowThatSwitchedDialogueOffBothWays_StaysOff()
        {
            // The retired codes only ever vote when the window saved them
            // ticked. Counting their absence as a vote would re-tick a channel
            // somebody switched off on purpose.
            var settings = NewWindowSettings();
            settings.ChatCodes = new List<ChatCodeViewModel>
            {
                new("003D", "NPCD", Color.FromArgb(255, 1, 2, 3), false),
                new("F03D", "NPCDRealtime", Color.FromArgb(255, 1, 2, 3), false)
            };

            WithWindow(settings, codes => Assert.That(IsTicked(codes, "003D"), Is.False));
        }

        [Test]
        public void AFreshWindow_AlsoTakesNarrationAndBossQuotes()
        {
            // Cutscene narration arrives under System rather than under a
            // dialogue code, so leaving it off loses whole scenes.
            WithFreshWindow(codes =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsTicked(codes, "0039"), Is.True, "narration");
                    Assert.That(IsTicked(codes, "2AB9"), Is.True, "boss quotes");
                });
            });
        }

        [Test]
        public void AFreshWindow_LeavesOtherPlayersAlone()
        {
            // Every ticked channel costs a request to a translation service per
            // line, and a city is a great many lines of other people talking.
            WithFreshWindow(codes =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsTicked(codes, "000A"), Is.False, "Say");
                    Assert.That(IsTicked(codes, "000E"), Is.False, "Party");
                    Assert.That(IsTicked(codes, "001B"), Is.False, "Novice Network");
                });
            });
        }

        [Test]
        public void ASavedWindow_KeepsWhatTheUserChose()
        {
            // The defaults are for a window nobody has had an opinion about
            // yet. Somebody who unticked the chat-log pair on purpose - because
            // they read fast and disliked the delayed copy - must keep it that
            // way.
            var settings = NewWindowSettings();
            settings.ChatCodes = new List<ChatCodeViewModel>
            {
                new("003D", "NPCD", Color.FromArgb(255, 1, 2, 3), false),
                new("000A", "Say", Color.FromArgb(255, 1, 2, 3), true)
            };

            WithWindow(settings, codes =>
            {
                Assert.Multiple(() =>
                {
                    Assert.That(IsTicked(codes, "003D"), Is.False, "unticked on purpose");
                    Assert.That(IsTicked(codes, "000A"), Is.True, "ticked on purpose");
                    Assert.That(IsTicked(codes, "0044"), Is.True, "not mentioned, so the default stands");
                });
            });
        }

        private static bool IsTicked(IEnumerable<ChatCodeViewModel> codes, string code)
        {
            var entry = codes.FirstOrDefault(x => string.Equals(x.Code, code, StringComparison.OrdinalIgnoreCase));

            Assert.That(entry, Is.Not.Null, "the window does not know the code " + code);

            return entry.IsChecked;
        }

        private static ChatWindowViewModelSettings NewWindowSettings()
        {
            return new ChatWindowViewModelSettings("1", 0)
            {
                TranslationEngineName = TranslationEngineName.GoogleTranslate,
                FromLanguague = Languages[0],
                ToLanguague = Languages[1]
            };
        }

        private static void WithFreshWindow(Action<IList<ChatCodeViewModel>> assertions)
        {
            WithWindow(NewWindowSettings(), assertions);
        }

        private static void WithWindow(
            ChatWindowViewModelSettings settings, Action<IList<ChatCodeViewModel>> assertions)
        {
            var engines = new List<TranslationEngine>
            {
                new(TranslationEngineName.GoogleTranslate, Languages, 1.0)
            };

            var logger = new NullLogger();
            var hotKeyManager = new HotKeyManager(null);
            var bindingService = new HotKeyBindingService(logger);

            try
            {
                var viewModel = new ChatWindowViewModel(
                    settings,
                    engines,
                    new TranslationCredentialsViewModel(new FakeCredentialStore()),
                    ShippedCodes(),
                    hotKeyManager,
                    logger,
                    bindingService);

                assertions(viewModel.ChatCodes);
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
            public bool IsEngineEnabled(TranslationEngineName engine) => true;

            public void SetEngineEnabled(TranslationEngineName engine, bool isEnabled)
            {
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
