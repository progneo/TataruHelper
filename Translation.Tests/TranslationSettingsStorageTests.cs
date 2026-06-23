using System.IO;

using NUnit.Framework;

using Translation.Settings;

namespace Translation.Tests
{
    [TestFixture]
    public class TranslationSettingsStorageTests
    {
        private string _path;

        [SetUp]
        public void SetUp()
        {
            _path = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName() + ".json");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path))
                File.Delete(_path);
        }

        [Test]
        public void Load_LegacyStaticFieldFormat_AppliesValuesByName()
        {
            // Shape produced by the old reflection-based SaveStaticToJson.
            File.WriteAllText(_path, @"[
                [""TranslationCacheSize"", 1234],
                [""UseGoogleJsonEndpoint"", false],
                [""SomeRemovedSetting"", ""ignored""],
                [""PapagoKeyCachePath"", ""Custom.cache""]
            ]");

            var settings = TranslationSettingsStorage.Load(_path);

            Assert.That(settings, Is.Not.Null);
            Assert.That(settings.TranslationCacheSize, Is.EqualTo(1234));
            Assert.That(settings.UseGoogleJsonEndpoint, Is.False);
            Assert.That(settings.PapagoKeyCachePath, Is.EqualTo("Custom.cache"));
            // Missing names keep their defaults.
            Assert.That(settings.HttpRequestRetryCount, Is.EqualTo(2));
        }

        [Test]
        public void Load_MissingFile_ReturnsNull()
        {
            Assert.That(TranslationSettingsStorage.Load(_path), Is.Null);
        }

        [Test]
        public void SaveThenLoad_RoundTripsValues()
        {
            var original = new TranslationSettings
            {
                TranslationCacheSize = 42,
                MaxSameLanguagePercent = 0.75,
                UseGoogleHtmlFallbackEndpoint = false,
                NTextCatLanguageModelsPath = "custom/path.xml"
            };

            Assert.That(TranslationSettingsStorage.Save(original, _path), Is.True);

            var loaded = TranslationSettingsStorage.Load(_path);

            Assert.That(loaded.TranslationCacheSize, Is.EqualTo(42));
            Assert.That(loaded.MaxSameLanguagePercent, Is.EqualTo(0.75));
            Assert.That(loaded.UseGoogleHtmlFallbackEndpoint, Is.False);
            Assert.That(loaded.NTextCatLanguageModelsPath, Is.EqualTo("custom/path.xml"));
        }
    }
}