using FFXIVTataruHelper;

using NUnit.Framework;

namespace TataruHelper.Tests
{
    public class AppRawDialogLogArgsTests
    {
        [TestCase("--log-raw-dialog")]
        [TestCase("/log-raw-dialog")]
        [TestCase("-log-raw-dialog")]
        [TestCase("--LOG-RAW-DIALOG")]
        [TestCase("  --log-raw-dialog  ")]
        public void ShouldEnableRawDialogLog_ReturnsTrue_ForRecognizedSwitch(string arg)
        {
            Assert.That(App.ShouldEnableRawDialogLog(new[] { "first", arg, "last" }), Is.True);
        }

        [Test]
        public void ShouldEnableRawDialogLog_ReturnsFalse_WhenSwitchAbsent()
        {
            Assert.That(App.ShouldEnableRawDialogLog(new[] { "--update", "C:/tmp" }), Is.False);
        }

        [Test]
        public void ShouldEnableRawDialogLog_ReturnsFalse_ForNullOrEmptyArgs()
        {
            Assert.That(App.ShouldEnableRawDialogLog(null), Is.False);
            Assert.That(App.ShouldEnableRawDialogLog(new string[0]), Is.False);
        }
    }
}