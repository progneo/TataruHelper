using FFXIVTataruHelper;
using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Services.UI;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;
using Updater;

namespace TataruHelper.Tests
{
    public class AppCompositionRootTests
    {
        [Test]
        public void ConfigureServices_RegistersCoreDependencies()
        {
            var services = new ServiceCollection();
            AppCompositionRoot.ConfigureServices(services);
            using (var provider = services.BuildServiceProvider())
            {
                Assert.That(provider.GetService<IAppLogger>(), Is.Not.Null);
                Assert.That(provider.GetService<ISettingsStore>(), Is.Not.Null);
                Assert.That(provider.GetService<IUiDispatcher>(), Is.Not.Null);
                Assert.That(provider.GetService<IGameMemoryGateway>(), Is.Not.Null);
                Assert.That(provider.GetService<IHotKeyBindingService>(), Is.Not.Null);
                Assert.That(provider.GetService<ITataruModelFactory>(), Is.Not.Null);
                Assert.That(provider.GetService<IFFMemoryReaderService>(), Is.Not.Null);
                Assert.That(provider.GetService<IUpdateService>(), Is.Not.Null);
            }
        }
    }
}
