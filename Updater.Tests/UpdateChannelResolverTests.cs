using NUnit.Framework;

namespace Updater.Tests
{
    [TestFixture]
    public class UpdateChannelResolverTests
    {
        [Test]
        public void ResolveExplicitChannel_ReturnsStable_ForRegularChecks()
        {
            var channel = UpdateChannelResolver.ResolveExplicitChannel(false);

            Assert.That(channel, Is.EqualTo("stable"));
        }

        [Test]
        public void ResolveExplicitChannel_ReturnsPrerelease_ForPrereleaseChecks()
        {
            var channel = UpdateChannelResolver.ResolveExplicitChannel(true);

            Assert.That(channel, Is.EqualTo("prerelease"));
        }
    }
}
