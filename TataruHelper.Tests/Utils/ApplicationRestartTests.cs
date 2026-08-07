using System.Linq;

using FFXIVTataruHelper.Utils;

using NUnit.Framework;

namespace TataruHelper.Tests.Utils
{
    // Only one copy of the application may run, held by a named mutex. A copy
    // started to take over from one that is closing must therefore wait for it
    // to go: started too early it finds the mutex taken, tells the old window
    // to show itself, and exits without a word - which looks exactly like a
    // reset button that does nothing.
    [TestFixture]
    public class ApplicationRestartTests
    {
        [Test]
        public void AnOrdinaryStart_WaitsForNobody()
        {
            Assert.That(ApplicationRestart.WaitsFor(new string[0]), Is.Zero);
            Assert.That(ApplicationRestart.WaitsFor(new[] { "--log-raw-dialog" }), Is.Zero);
            Assert.That(ApplicationRestart.WaitsFor(null), Is.Zero);
        }

        [Test]
        public void TheProcessToWaitFor_IsRead()
        {
            Assert.That(ApplicationRestart.WaitsFor(new[] { "--wait-for-exit", "4242" }), Is.EqualTo(4242));
        }

        [Test]
        public void SomethingThatIsNotAProcess_IsNotWaitedFor()
        {
            // Waiting on nonsense would hold the start up for the whole timeout
            // and then carry on anyway.
            Assert.That(ApplicationRestart.WaitsFor(new[] { "--wait-for-exit", "later" }), Is.Zero);
            Assert.That(ApplicationRestart.WaitsFor(new[] { "--wait-for-exit" }), Is.Zero);
            Assert.That(ApplicationRestart.WaitsFor(new[] { "--wait-for-exit", "0" }), Is.Zero);
        }

        [Test]
        public void TheOtherArgumentsAreCarriedOver()
        {
            // Somebody debugging with the raw dialogue log on should still have
            // it after a reset.
            var carried = ApplicationRestart.Carry(new[] { "--log-raw-dialog", "-prerelease" }).ToArray();

            Assert.That(carried, Is.EqualTo(new[] { "--log-raw-dialog", "-prerelease" }));
        }

        [Test]
        public void AWaitFromTheRestartBefore_IsNotCarriedOver()
        {
            // Otherwise each reset would leave the next copy waiting on a
            // process that went long ago.
            var carried = ApplicationRestart.Carry(
                new[] { "--wait-for-exit", "4242", "--log-raw-dialog" }).ToArray();

            Assert.That(carried, Is.EqualTo(new[] { "--log-raw-dialog" }));
        }
    }
}
