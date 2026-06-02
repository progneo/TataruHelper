using FFXIVTataruHelper.Services.Settings;

using NUnit.Framework;

using System;
using System.IO;

namespace TataruHelper.Tests
{
    public class AppSettingsStoreTests
    {
        [Test]
        public void Paths_AreStableAndUseProvidedDirectories()
        {
            var appData = Path.Combine(Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"), "appdata");
            var baseDir = Path.Combine(Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"), "basedir");

            Directory.CreateDirectory(appData);
            Directory.CreateDirectory(baseDir);

            var sut = new AppSettingsStore(appData, baseDir);

            Assert.That(sut.SettingsPath, Is.EqualTo(Path.Combine(appData, "UserSettingsNew.json")));
            Assert.That(sut.OldSettingsPath, Is.EqualTo(Path.Combine(appData, "UserSettings.json")));
            Assert.That(sut.SystemSettingsPath, Is.EqualTo(Path.Combine(appData, "AppSysSettings.json")));
            Assert.That(Path.IsPathRooted(sut.ChatCodesFilePath), Is.True);
            Assert.That(Path.IsPathRooted(sut.BlackListPath), Is.True);
            Assert.That(Path.IsPathRooted(sut.IgnoreNickNameChatCodesPath), Is.True);
        }

        [Test]
        public void Migration_CopiesLegacySettings_WhenNewSettingsDoesNotExist()
        {
            var root = Path.Combine(Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"));
            var appData = Path.Combine(root, "appdata");
            var baseDir = Path.Combine(root, "base");
            Directory.CreateDirectory(appData);
            Directory.CreateDirectory(baseDir);

            var legacySettingsPath = Path.GetFullPath(Path.Combine(baseDir, "../UserSettingsNew.json"));
            var legacyDir = Path.GetDirectoryName(legacySettingsPath);
            if (!string.IsNullOrEmpty(legacyDir))
            {
                Directory.CreateDirectory(legacyDir);
            }

            File.WriteAllText(legacySettingsPath, "{ \"migration\": true }");

            var sut = new AppSettingsStore(appData, baseDir);

            Assert.That(File.Exists(sut.SettingsPath), Is.True);
            var content = File.ReadAllText(sut.SettingsPath);
            Assert.That(content.Contains("\"migration\": true"), Is.True);
        }
    }
}