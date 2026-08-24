using FFXIVTataruHelper;

using NUnit.Framework;

using System;
using System.Collections.Generic;
using System.IO;

namespace TataruHelper.Tests
{
    /// <summary>
    /// The two failure modes of the settings file: a save that dies halfway
    /// must not leave a truncated file, and a file that will not read must
    /// survive the attempt to load it - quarantined, not overwritten.
    /// </summary>
    public class HelperSettingsFileTests
    {
        private static string NewPath()
        {
            var directory = Path.Combine(
                Path.GetTempPath(), "tataru-tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            return Path.Combine(directory, "settings.json");
        }

        [Test]
        public void ACompletedSave_RoundTrips()
        {
            var path = NewPath();

            Helper.SaveJson(new List<string> { "one", "two" }, path);

            var reloaded = Helper.LoadJsonData<List<string>>(path);

            Assert.That(reloaded, Is.EqualTo(new List<string> { "one", "two" }));
            Assert.That(File.Exists(path + ".new"), Is.False, "the side file must be moved away");
        }

        [Test]
        public void AFileThatWillNotRead_IsKeptUntouchedAside()
        {
            var path = NewPath();
            var onDisk = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
            File.WriteAllBytes(path, onDisk);

            Helper.LoadJsonData<List<string>>(path);

            Assert.That(File.Exists(path), Is.False, "the unreadable file must be moved aside");
            Assert.That(File.ReadAllBytes(path + ".corrupt"), Is.EqualTo(onDisk));
        }
    }
}