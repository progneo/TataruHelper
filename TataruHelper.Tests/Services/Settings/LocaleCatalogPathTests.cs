using System;
using System.IO;
using System.Linq;
using System.Reflection;

using FFXIVTataruHelper;
using FFXIVTataruHelper.Services.Settings;

using NUnit.Framework;

namespace TataruHelper.Tests.Services.Settings
{
    // The catalog layout is named after Crowdin's %locale% on both sides, so a
    // downloaded translation lands exactly where the app reads it. Nothing at
    // runtime complains when it does not - a missing catalog silently falls
    // back to English - so the layout is pinned here instead.
    [TestFixture]
    public class LocaleCatalogPathTests
    {
        private string _tempDir;

        [SetUp]
        public void SetUp()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "TataruLocaleTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [TearDown]
        public void TearDown()
        {
            try
            {
                Directory.Delete(_tempDir, true);
            }
            catch (IOException)
            {
            }
        }

        [Test]
        public void EveryLocalePath_PointsAtACatalogThatShips()
        {
            var localeRoot = Path.Combine(FindRepositoryRoot(), "TataruHelper", "Locale");
            var settings = new AppSettings();

            foreach (var property in LocalePathProperties())
            {
                var catalog = Path.Combine(localeRoot, (string)property.GetValue(settings));
                Assert.That(File.Exists(catalog), Is.True,
                    $"{property.Name} points at '{catalog}', which is not in the repository.");
            }
        }

        [Test]
        public void EveryLocalePath_IsNamedAfterItsCrowdinLocale()
        {
            var settings = new AppSettings();

            foreach (var property in LocalePathProperties())
            {
                var path = (string)property.GetValue(settings);
                var folder = Path.GetDirectoryName(path);
                var file = Path.GetFileNameWithoutExtension(path);

                Assert.That(file, Is.EqualTo(folder),
                    $"{property.Name} is '{path}'; Crowdin substitutes the same %locale% for both parts, "
                    + "so folder and file name have to match or the round trip breaks.");
            }
        }

        // The renamed catalogs still have to parse: NGettext reads the binary
        // .mo, and a truncated or mis-copied file loads as an empty catalog
        // that silently answers with the untranslated msgid.
        [Test]
        public void EveryLocalePath_LoadsAsAGettextCatalog()
        {
            var localeRoot = Path.Combine(FindRepositoryRoot(), "TataruHelper", "Locale");
            var settings = new AppSettings();

            foreach (var property in LocalePathProperties())
            {
                var path = Path.Combine(localeRoot, (string)property.GetValue(settings));

                using (var stream = File.OpenRead(path))
                {
                    var catalog = new NGettext.Catalog(stream);
                    Assert.That(catalog.Translations, Is.Not.Empty, $"{path} loaded as an empty catalog.");
                }
            }
        }

        [Test]
        public void RussianCatalog_StillResolvesItsTranslations()
        {
            var settings = new AppSettings();
            var path = Path.Combine(FindRepositoryRoot(), "TataruHelper", "Locale",
                settings.ru_RU_LanguaguePath);

            using (var stream = File.OpenRead(path))
            {
                var catalog = new NGettext.Catalog(stream);
                Assert.That(catalog.GetString("Settings"), Is.EqualTo("Настройки"));
            }
        }

        // AppSysSettings.json is written on every run, so it pins whichever
        // layout shipped at the time and outlives a change to the defaults.
        [Test]
        public void LoadGlobalSettings_StaleLocalePath_FallsBackToTheDefault()
        {
            var baseDirectory = CreateBaseDirectoryWithCatalogs();
            var appData = Path.Combine(_tempDir, "appdata");
            Directory.CreateDirectory(appData);

            var stale = new AppSettings { ru_RU_LanguaguePath = @"ru\ru_RU.mo" };
            SaveGlobalSettings(stale, Path.Combine(appData, "AppSysSettings.json"));

            var store = new AppSettingsStore(appData, baseDirectory);
            Assert.That(store.LoadGlobalSettings("AppSysSettings.json"), Is.True);

            Assert.That(store.AppSettings.ru_RU_LanguaguePath,
                Is.EqualTo(new AppSettings().ru_RU_LanguaguePath));
        }

        [Test]
        public void LoadGlobalSettings_CustomLocalePathThatExists_IsKept()
        {
            var baseDirectory = CreateBaseDirectoryWithCatalogs();
            var appData = Path.Combine(_tempDir, "appdata");
            Directory.CreateDirectory(appData);

            var custom = Path.Combine("custom", "mine.mo");
            var customFull = Path.Combine(baseDirectory, "Locale", custom);
            Directory.CreateDirectory(Path.GetDirectoryName(customFull));
            File.WriteAllBytes(customFull, Array.Empty<byte>());

            SaveGlobalSettings(new AppSettings { ru_RU_LanguaguePath = custom },
                Path.Combine(appData, "AppSysSettings.json"));

            var store = new AppSettingsStore(appData, baseDirectory);
            store.LoadGlobalSettings("AppSysSettings.json");

            Assert.That(store.AppSettings.ru_RU_LanguaguePath, Is.EqualTo(custom));
        }

        private static PropertyInfo[] LocalePathProperties()
        {
            var properties = typeof(AppSettings).GetProperties()
                .Where(x => x.Name.EndsWith("LanguaguePath", StringComparison.Ordinal))
                .ToArray();

            Assert.That(properties, Is.Not.Empty, "No locale path properties found on AppSettings.");
            return properties;
        }

        // Lays out the catalogs the defaults expect, so only the path under
        // test is missing.
        private string CreateBaseDirectoryWithCatalogs()
        {
            var baseDirectory = Path.Combine(_tempDir, "app");
            var defaults = new AppSettings();

            foreach (var property in LocalePathProperties())
            {
                var catalog = Path.Combine(baseDirectory, defaults.LocalisationDirPath,
                    (string)property.GetValue(defaults));
                Directory.CreateDirectory(Path.GetDirectoryName(catalog));
                File.WriteAllBytes(catalog, Array.Empty<byte>());
            }

            return baseDirectory;
        }

        private static void SaveGlobalSettings(AppSettings settings, string path)
        {
            var store = new AppSettingsStore(Path.GetDirectoryName(path), Path.GetDirectoryName(path));
            typeof(AppSettingsStore)
                .GetProperty(nameof(AppSettingsStore.AppSettings))
                .SetValue(store, settings);
            store.SaveGlobalSettings(Path.GetFileName(path));
        }

        private static string FindRepositoryRoot()
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);

            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "TataruHelper.sln")))
                directory = directory.Parent;

            Assert.That(directory, Is.Not.Null, "Could not locate the repository root from the test assembly.");
            return directory.FullName;
        }
    }
}
