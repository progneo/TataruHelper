using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.Services.Update;
using Microsoft.Extensions.DependencyInjection;
using System;
using Translation;
using Updater;

namespace FFXIVTataruHelper
{
    public static class AppCompositionRoot
    {
        public static IServiceProvider BuildServiceProvider()
        {
            var services = new ServiceCollection();
            ConfigureServices(services);
            return services.BuildServiceProvider();
        }

        public static void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<IAppLogger, AppLogger>();
            services.AddSingleton<ISettingsStore, AppSettingsStore>();
            services.AddSingleton<ISettingsSyncService, SettingsSyncService>();
            services.AddSingleton<ISettingsMigrationService, SettingsMigrationService>();
            services.AddSingleton<IUiDispatcher, WpfUiDispatcher>();

            services.AddSingleton<IDirectDialogReader, HeuristicDirectDialogReader>();
            services.AddSingleton<IGameMemoryGateway, SharlayanGameMemoryGateway>();

            services.AddSingleton<Translation.ILog>(provider => new Utils.LoggerWrapper(provider.GetRequiredService<IAppLogger>()));
            services.AddSingleton<WebTranslator>(provider => new WebTranslator(provider.GetRequiredService<Translation.ILog>()));
            services.AddSingleton<IFFMemoryReaderService, FFMemoryReader>();

            services.AddSingleton<IHotKeyBindingService, HotKeyBindingService>();
            services.AddSingleton<IChatWindowFactory, ChatWindowFactory>();
            services.AddTransient<IChatWindowCoordinator, ChatWindowCoordinator>();
            services.AddTransient<IChatWindowsEventCoordinator, ChatWindowsEventCoordinator>();
            services.AddTransient<ITranslationPipelineCoordinator, TranslationPipelineCoordinator>();
            services.AddTransient<IApplicationCoordinator, ApplicationCoordinator>();
            services.AddTransient<ITataruModelFactory, TataruModelFactory>();

            services.AddSingleton<IUpdateService>(provider =>
            {
                var logger = provider.GetRequiredService<IAppLogger>();
                try
                {
                    return (IUpdateService)new VelopackUpdateService(new Utils.LoggerWrapper(logger));
                }
                catch (Exception ex)
                {
                    logger.WriteLog("Failed to initialize Velopack updater. Updater will run in disabled mode.");
                    logger.WriteLog(ex);
                    return new DisabledUpdateService();
                }
            });
            services.AddTransient<MainWindow>();
        }
    }
}
