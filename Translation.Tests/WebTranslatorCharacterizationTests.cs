using System;
using System.Collections.Generic;

using NUnit.Framework;

namespace Translation.Tests
{
    [TestFixture]
    public class WebTranslatorCharacterizationTests
    {
        [Test]
        public void Translate_Success_ReturnsTextFromSelectedEngine()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "translated value");
            var translator = new WebTranslator(new NullLog(), new[] { googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("Spanish", "Spanish", "es");

            var result = translator.Translate("Hello world", engine, from, to);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Text, Is.EqualTo("translated value"));
            Assert.That(result.Engine, Is.EqualTo(TranslationEngineName.GoogleTranslate));
        }

        [Test]
        public void Translate_UsesCache_ForRepeatedRequest()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "translated value");
            var translator = new WebTranslator(new NullLog(), new[] { googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("Spanish", "Spanish", "es");

            translator.Translate("Hello world", engine, from, to);
            translator.Translate("Hello world", engine, from, to);

            Assert.That(googleProvider.CallCount, Is.EqualTo(1));
        }

        [Test]
        public void Translate_DoesNotFallBack_WhenProviderThrows()
        {
            var failingDeepL = new FakeProvider(TranslationEngineName.DeepL, null, true);
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator = new WebTranslator(new NullLog(),
                new ITranslationProvider[] { failingDeepL, googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.DeepL);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("German", "German", "de");

            var result = translator.Translate("Hello", engine, from, to);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.Engine, Is.EqualTo(TranslationEngineName.DeepL));
            Assert.That(result.FailureKind, Is.EqualTo(TranslationFailureKind.ProviderException));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void Translate_FlagsEmptyResponse_WithoutFallback()
        {
            var emptyProvider = new FakeProvider(TranslationEngineName.DeepL, string.Empty);
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator = new WebTranslator(new NullLog(),
                new ITranslationProvider[] { emptyProvider, googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.DeepL);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("German", "German", "de");

            var result = translator.Translate("Hello", engine, from, to);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(TranslationFailureKind.EmptyResponse));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void Translate_ReportsProviderUnavailable_WhenEngineNotRegistered()
        {
            var deepLProvider = new FakeProvider(TranslationEngineName.DeepL, "unused");
            var translator = new WebTranslator(new NullLog(), new[] { deepLProvider }, false);

            var engine = CreateEngine(TranslationEngineName.Baidu);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("French", "French", "fr");

            var result = translator.Translate("Hello", engine, from, to);

            Assert.That(result.IsSuccess, Is.False);
            Assert.That(result.FailureKind, Is.EqualTo(TranslationFailureKind.ProviderUnavailable));
            Assert.That(deepLProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void Translate_ReturnsOriginal_WhenTargetLanguageEqualsSourceLanguage()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator = new WebTranslator(new NullLog(), new[] { googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var sameLang = new TranslatorLanguague("English", "English", "en");

            var result = translator.Translate("No translation needed", engine, sameLang, sameLang);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Text, Is.EqualTo("No translation needed"));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
        }

        [Test]
        public void Translate_ReturnsOriginal_WhenInputHasNoLetters()
        {
            var googleProvider = new FakeProvider(TranslationEngineName.GoogleTranslate, "should not be used");
            var translator = new WebTranslator(new NullLog(), new[] { googleProvider }, false);

            var engine = CreateEngine(TranslationEngineName.GoogleTranslate);
            var from = new TranslatorLanguague("English", "English", "en");
            var to = new TranslatorLanguague("Spanish", "Spanish", "es");

            var result = translator.Translate("12345 !!! ???", engine, from, to);

            Assert.That(result.IsSuccess, Is.True);
            Assert.That(result.Text, Is.EqualTo("12345 !!! ???"));
            Assert.That(googleProvider.CallCount, Is.EqualTo(0));
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