using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using Translation.Models;
using Translation.Settings;

namespace Translation.Tests
{
    [TestFixture]
    public class WebTranslatorCharacterizationTests
    {
        [Test]
        public async Task Translate_Success_ReturnsTextFromSelectedEngine()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "translated value");
            var translator =
                new WebTranslator(NullLogger.Instance, new[] { googleProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("Spanish", "Spanish", "es");

            var result = await translator.TranslateAsync("Hello world", engine, from, to);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Text, Is.EqualTo("translated value"));
            Assert.That(result.Engine, Is.EqualTo(TranslationEngineName.GoogleTranslate));
        }

        [Test]
        public async Task Translate_UsesCache_ForRepeatedRequest()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "translated value");
            var translator =
                new WebTranslator(NullLogger.Instance, new[] { googleProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("Spanish", "Spanish", "es");

            await translator.TranslateAsync("Hello world", engine, from, to);
            await translator.TranslateAsync("Hello world", engine, from, to);

            Assert.That(googleProvider.CallCount, Is.EqualTo(1));
        }

        [Test]
        public async Task Translate_DoesNotFallBack_WhenProviderThrows()
        {
            var failingDeepL = new FakeProvider(TranslationEngineName.DeepLApi, null, true);
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator = new WebTranslator(NullLogger.Instance,
                new ITranslationProvider[] { failingDeepL, googleProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.DeepLApi);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("German", "German", "de");

            var result = await translator.TranslateAsync("Hello", engine, from, to);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Engine, Is.EqualTo(TranslationEngineName.DeepLApi));
            Assert.That(result.FailureKind, Is.EqualTo(TranslationFailureKind.ProviderException));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Translate_FlagsEmptyResponse_WithoutFallback()
        {
            var emptyProvider = new FakeProvider(TranslationEngineName.DeepLApi, string.Empty);
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator = new WebTranslator(NullLogger.Instance,
                new ITranslationProvider[] { emptyProvider, googleProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.DeepLApi);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("German", "German", "de");

            var result = await translator.TranslateAsync("Hello", engine, from, to);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(TranslationFailureKind.EmptyResponse));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Translate_ReportsProviderUnavailable_WhenEngineNotRegistered()
        {
            var deepLProvider = new FakeProvider(TranslationEngineName.DeepLApi, "unused");
            var translator = new WebTranslator(NullLogger.Instance, new[] { deepLProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.Papago);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("French", "French", "fr");

            var result = await translator.TranslateAsync("Hello", engine, from, to);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(TranslationFailureKind.ProviderUnavailable));
            Assert.That(deepLProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Translate_ReturnsOriginal_WhenTargetLanguageEqualsSourceLanguage()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator =
                new WebTranslator(NullLogger.Instance, new[] { googleProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var sameLang = new TranslatorLanguage("English", "English", "en");

            var result = await translator.TranslateAsync("No translation needed", engine, sameLang, sameLang);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Text, Is.EqualTo("No translation needed"));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task Translate_ReturnsOriginal_WhenInputHasNoLetters()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator =
                new WebTranslator(NullLogger.Instance, new[] { googleProvider }, new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("Spanish", "Spanish", "es");

            var result = await translator.TranslateAsync("12345 !!! ???", engine, from, to);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Text, Is.EqualTo("12345 !!! ???"));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public async Task TranslateAsync_ParallelRequests_DoNotCorruptCache()
        {
            var provider = new ConcurrentFakeProvider(TranslationEngineName.GoogleTranslate);
            var translator = new WebTranslator(NullLogger.Instance, new[] { (ITranslationProvider)provider },
                new TranslationSettings());

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguage("English", "English", "en");
            var to = new TranslatorLanguage("Spanish", "Spanish", "es");

            var sentences = new string[10];
            for (var i = 0; i < sentences.Length; i++)
                sentences[i] = "sentence number " + i;

            foreach (var sentence in sentences)
                await translator.TranslateAsync(sentence, engine, from, to);

            var callsAfterWarmup = provider.CallCount;
            Assert.That(callsAfterWarmup, Is.EqualTo(sentences.Length));

            var tasks = new List<Task<TranslationResult>>();
            for (var round = 0; round < 20; round++)
            {
                foreach (var sentence in sentences)
                    tasks.Add(translator.TranslateAsync(sentence, engine, from, to));
            }

            Assert.That(() => Task.WhenAll(tasks), Throws.Nothing);

            foreach (var task in tasks)
            {
                Assert.That(task.Result.IsSuccess, Is.True);
                Assert.That(task.Result.Text, Does.StartWith("translated:"));
            }

            Assert.That(provider.CallCount, Is.EqualTo(callsAfterWarmup));
        }

        private static TranslationEngine CreateEngine(TranslationEngineName engineName)
        {
            return new TranslationEngine(
                engineName,
                new List<TranslatorLanguage>
                {
                    new TranslatorLanguage("English", "English", "en"),
                    new TranslatorLanguage("Spanish", "Spanish", "es"),
                    new TranslatorLanguage("German", "German", "de"),
                    new TranslatorLanguage("French", "French", "fr")
                },
                10);
        }

        private sealed class FakeProvider : ITranslationProvider
        {
            private readonly string _response;
            private readonly bool _throwOnCall;

            public TranslationEngineName EngineName { get; private set; }
            public int CallCount { get; private set; }

            public FakeProvider(TranslationEngineName engineName, string response, bool throwOnCall = false)
            {
                EngineName = engineName;
                _response = response;
                _throwOnCall = throwOnCall;
            }

            public Task<string> TranslateAsync(string sentence, string inLang,
                string outLang, CancellationToken cancellationToken)
            {
                CallCount++;

                if (_throwOnCall)
                    throw new InvalidOperationException("Provider failure");

                return Task.FromResult(_response ?? string.Empty);
            }
        }

        private sealed class ConcurrentFakeProvider : ITranslationProvider
        {
            private int _callCount;

            public TranslationEngineName EngineName { get; private set; }
            public int CallCount => Volatile.Read(ref _callCount);

            public ConcurrentFakeProvider(TranslationEngineName engineName)
            {
                EngineName = engineName;
            }

            public Task<string> TranslateAsync(string sentence, string inLang,
                string outLang, CancellationToken cancellationToken)
            {
                Interlocked.Increment(ref _callCount);
                return Task.FromResult("translated:" + sentence);
            }
        }
    }
}