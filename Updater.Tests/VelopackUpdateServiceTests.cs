using System;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

namespace Updater.Tests
{
    [TestFixture]
    public class VelopackUpdateServiceTests
    {
        [Test]
        public void IsUpdating_IsFalse_AfterConstruction()
        {
            using (var service = new VelopackUpdateService(NullLogger.Instance))
            {
                Assert.That(service.IsUpdating, Is.False);
            }
        }

        [Test]
        public void Dispose_IsIdempotent()
        {
            var service = new VelopackUpdateService(NullLogger.Instance);

            service.Dispose();

            Assert.That(() => service.Dispose(), Throws.Nothing);
            Assert.That(service.IsUpdating, Is.False);
        }

        [Test]
        public void StopUpdate_WithoutActiveUpdate_DoesNotThrow()
        {
            using (var service = new VelopackUpdateService(NullLogger.Instance))
            {
                Assert.That(() => service.StopUpdate(), Throws.Nothing);
            }
        }

        [Test]
        public void Dispose_ConcurrentWithStopUpdate_DoesNotThrow()
        {
            for (var i = 0; i < 50; i++)
            {
                var service = new VelopackUpdateService(NullLogger.Instance);

                var stopTask = Task.Run(() => service.StopUpdate());
                var disposeTask = Task.Run(() => service.Dispose());

                Assert.That(() => Task.WaitAll(stopTask, disposeTask), Throws.Nothing);
            }
        }

        [Test]
        public void CheckAndInstallUpdatesAsync_AfterDispose_Throws()
        {
            var service = new VelopackUpdateService(NullLogger.Instance);
            service.Dispose();

            Assert.That(
                async () => await service.CheckAndInstallUpdatesAsync(false, default),
                Throws.InstanceOf<ObjectDisposedException>());
        }
    }
}
