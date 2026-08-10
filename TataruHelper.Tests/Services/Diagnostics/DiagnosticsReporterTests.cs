using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.Diagnostics;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Update;
using FFXIVTataruHelper.ViewModel;

using NUnit.Framework;

using Translation.Credentials;
using Translation.Models;
using Translation.Reference;

using Color = System.Windows.Media.Color;

namespace TataruHelper.Tests.Services.Diagnostics
{
    // Reads a real chat window rather than a stand-in, because what is being
    // checked is that the report reaches for the right things on it. Every
    // mistake possible here is a property name that compiles and reports
    // nothing useful.
    [TestFixture]
    public class DiagnosticsReporterTests
    {
        [Test]
        public void ReportsWhatEachWindowIsSetTo()
        {
            WithWindow(window =>
            {
                var report = Collect(window);

                Assert.Multiple(() =>
                {
                    Assert.That(report, Does.Contain("GoogleTranslate"), "the engine");
                    Assert.That(report, Does.Contain("English -> Russian"), "the pair");
                    Assert.That(report, Does.Contain("003D"), "the ticked codes");
                    Assert.That(report, Does.Contain("NPC dialogue yes"));
                });
            });
        }

        [Test]
        public void ReportsAWindowThatWouldShowNoDialogue()
        {
            WithWindow(window =>
            {
                foreach (var code in window.ChatCodes)
                {
                    code.IsChecked = false;
                }

                var report = Collect(window);

                Assert.Multiple(() =>
                {
                    Assert.That(report, Does.Contain("NPC dialogue no"));
                    Assert.That(report, Does.Contain("ticked: nothing"));
                    Assert.That(report, Does.Contain("not listening to"));
                });
            });
        }

        [Test]
        public void ReportsTheInstalledTranslationIndex()
        {
            WithWindow(window =>
            {
                var report = Collect(window, new FakeReferenceIndex());

                Assert.That(report, Does.Contain("English -> Russian, 201267 lines, rules v5, revision abc1234"));
            });
        }

        [Test]
        public void SurvivesEverythingBeingUnavailable()
        {
            var reporter = new DiagnosticsReporter(
                () => throw new InvalidOperationException("no reader"),
                null,
                () => throw new InvalidOperationException("no windows"),
                () => throw new InvalidOperationException("no flag"),
                () => throw new InvalidOperationException("no language"),
                "v1.0.4",
                new NullLogger());

            var (report, _) = reporter.Collect();

            Assert.Multiple(() =>
            {
                Assert.That(report, Does.Contain("v1.0.4"), "what could be read still gets out");
                Assert.That(report, Does.Contain("not attached"));
                Assert.That(report, Does.Contain("not available in this build"));
            });
        }

        private static string Collect(
            ChatWindowViewModel window, IReferenceIndexUpdateService referenceIndex = null)
        {
            var reporter = new DiagnosticsReporter(
                () => new GameReadingDiagnostics(
                    true, "ffxiv_dx11.exe  PID: 4242", "en", true, true, 12, new[] { "003D" }),
                referenceIndex,
                () => new[] { window },
                () => true,
                () => "Russian",
                "v1.0.4",
                new NullLogger());

            var (report, _) = reporter.Collect();
            return report;
        }

        private static readonly List<TranslatorLanguage> Languages = new()
        {
            new TranslatorLanguage("English", "English", "en"),
            new TranslatorLanguage("Russian", "Russian", "ru")
        };

        private static void WithWindow(Action<ChatWindowViewModel> assertions)
        {
            var settings = new ChatWindowViewModelSettings("1", 0)
            {
                TranslationEngineName = TranslationEngineName.GoogleTranslate,
                FromLanguague = Languages[0],
                ToLanguague = Languages[1]
            };

            var white = Color.FromArgb(255, 255, 255, 255);
            var shippedCodes = new List<ChatMsgType>
            {
                new("0039", MsgType.Translate, "System", white),
                new("003D", MsgType.Translate, "NPCD", white),
                new("0044", MsgType.Translate, "NPCA", white),
                new("000A", MsgType.Skip, "Say", white)
            };

            var logger = new NullLogger();
            var hotKeyManager = new HotKeyManager(null);

            try
            {
                var window = new ChatWindowViewModel(
                    settings,
                    new List<TranslationEngine> { new(TranslationEngineName.GoogleTranslate, Languages, 1.0) },
                    new TranslationCredentialsViewModel(new FakeCredentialStore()),
                    shippedCodes,
                    hotKeyManager,
                    logger,
                    new HotKeyBindingService(logger));

                assertions(window);
            }
            finally
            {
                hotKeyManager.Dispose();
            }
        }

        private sealed class FakeReferenceIndex : IReferenceIndexUpdateService
        {
            public bool IsSupported => true;

            public ReferenceIndexState ReadState() =>
                new ReferenceIndexState(true, "English", "Russian", "abc1234", 201267, 5);

            public string GameLanguage => "English";

            public (string GameLanguage, string ReadingLanguage) ResolveLanguages(
                string gameLanguage, string readingLanguage) => (gameLanguage, readingLanguage);

            public Task<string> GetLatestRevisionAsync(CancellationToken cancellationToken) =>
                Task.FromResult(string.Empty);

            public Task<ReferenceUpdateResult> UpdateAsync(
                string gameLanguage,
                string readingLanguage,
                IProgress<ReferenceUpdateProgress> progress,
                CancellationToken cancellationToken) =>
                throw new NotSupportedException();
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
            public bool IsEngineEnabled(TranslationEngineName engine) => true;
            public void SetEngineEnabled(TranslationEngineName engine, bool isEnabled) { }
            public string GetApiKey(TranslationEngineName engine) => string.Empty;
            public string GetRegion(TranslationEngineName engine) => string.Empty;
            public string GetModel(TranslationEngineName engine) => string.Empty;
            public void SetApiKey(TranslationEngineName engine, string apiKey) { }
            public void SetRegion(TranslationEngineName engine, string region) { }
            public void SetModel(TranslationEngineName engine, string model) { }
            public void Save() { }
        }
    }
}
