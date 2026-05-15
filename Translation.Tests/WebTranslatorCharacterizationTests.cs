using System;
using System.Collections.Generic;
using NUnit.Framework;

namespace Translation.Tests
{
    [TestFixture]
    public class WebTranslatorCharacterizationTests
    {
        [Test]
        public void Translate_UsesCache_ForSameRequest()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "translated value");
            var translator = new WebTranslator(new NullLog(), new[] { googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("Spanish", "Spanish", "es");

            var first = translator.Translate("Hello world", engine, from, to);
            var second = translator.Translate("Hello world", engine, from, to);

            Assert.That(first, Is.EqualTo("translated value"));
            Assert.That(second, Is.EqualTo("translated value"));
            Assert.That(googleProvider.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Translate_FallsBackToGoogle_WhenPrimaryProviderFails()
        {
            var failingDeepL = new FakeProvider(TranslationEngineName.DeepL, null, true);
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "google fallback");
            var translator = new WebTranslator(new NullLog(), new ITranslationProvider[] { failingDeepL, googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.DeepL);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("German", "German", "de");

            var result = translator.Translate("Hello", engine, from, to);

            Assert.That(result, Is.EqualTo("google fallback"));
            Assert.That(failingDeepL.CallCount, Is.EqualTo(1));
            Assert.That(googleProvider.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Translate_ReturnsEmpty_WhenProviderIsMissingAndNoFallback()
        {
            var deepLProvider = new FakeProvider(TranslationEngineName.DeepL, "unused");
            var translator = new WebTranslator(new NullLog(), new[] { deepLProvider }, false);

            var engine = CreateEngine(TranslationEngineName.Baidu);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("French", "French", "fr");

            var result = translator.Translate("Hello", engine, from, to);

            Assert.That(result, Is.Empty);
            Assert.That(deepLProvider.CallCount, Is.EqualTo(0));
        }

        private static TranslationEngine CreateEngine(TranslationEngineName engineName)
        {
            return new TranslationEngine(
                engineName,
                new List<TranslatorLanguague>
                {
                    new TranslatorLanguague("English", "English", "en"),
                    new TranslatorLanguague("Spanish", "Spanish", "es"),
                    new TranslatorLanguague("German", "German", "de"),
                    new TranslatorLanguague("French", "French", "fr")
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

            public string Translate(string sentence, string inLang, string outLang)
            {
                CallCount++;

                if (_throwOnCall)
                    throw new InvalidOperationException("Provider failure");

                return _response ?? string.Empty;
            }
        }

        private sealed class NullLog : ILog
        {
            public void WriteLog(string inputString, string memberName = "", int sourceLineNumber = 0)
            {
            }
        }
    }
}
