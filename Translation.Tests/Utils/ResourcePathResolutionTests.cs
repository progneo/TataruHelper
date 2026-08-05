using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using NUnit.Framework;

using Translation.Models;
using Translation.Settings;

namespace Translation.Tests.Utils
{
    // The language catalogs ship next to the executable and are named by
    // relative paths, so they load correctly only while the working directory
    // is the executable's folder. It is not when the process is elevated -
    // Windows hands it System32 - nor when it is started from a shortcut with
    // its own "start in". Nothing surfaces the failure: the loader substitutes
    // an empty list and every language picker comes up blank.
    [TestFixture]
    public class ResourcePathResolutionTests
    {
        private string _originalDirectory;
        private string _elsewhere;

        [SetUp]
        public void SetUp()
        {
            _originalDirectory = Directory.GetCurrentDirectory();
            _elsewhere = Path.Combine(Path.GetTempPath(), "TataruCwd_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_elsewhere);
        }

        [TearDown]
        public void TearDown()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
            try
            {
                Directory.Delete(_elsewhere, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void Languages_LoadFromAnyWorkingDirectory()
        {
            var settings = new TranslationSettings();
            Assume.That(File.Exists(Path.Combine(AppContext.BaseDirectory, settings.GoogleTranslateLanguages)),
                Is.True, "the catalog has to ship next to the assembly for this to mean anything");

            Directory.SetCurrentDirectory(_elsewhere);

            var languages = LoadLanguages(settings.GoogleTranslateLanguages);

            Assert.That(languages, Is.Not.Empty,
                "an empty catalog is what leaves the language pickers blank");
            Assert.That(languages.Any(x => x.SystemName == "English"), Is.True);
        }

        [Test]
        public void Languages_StillLoadFromTheExecutableDirectory()
        {
            var settings = new TranslationSettings();
            Directory.SetCurrentDirectory(AppContext.BaseDirectory);

            Assert.That(LoadLanguages(settings.YandexLanguages), Is.Not.Empty);
        }

        [Test]
        public void Languages_MissingCatalogStillYieldsAnEmptyList()
        {
            Directory.SetCurrentDirectory(_elsewhere);

            Assert.That(LoadLanguages("TranslationResources/NoSuchCatalog.json"), Is.Empty);
        }

        // JsonDataLoader is internal to the Translation assembly, and the test
        // project sees it through InternalsVisibleTo.
        private static List<TranslatorLanguage> LoadLanguages(string path)
        {
            return global::Translation.Utils.JsonDataLoader.LoadJsonData<List<TranslatorLanguage>>(path);
        }
    }
}
