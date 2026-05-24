using System;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.ViewModel;

using Translation.Core;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class ApplicationCoordinator : IApplicationCoordinator
    {
        private static readonly TimeSpan SettingsShutdownTimeout = TimeSpan.FromSeconds(5);

        private readonly IFFMemoryReaderService _ffMemoryReader;
        private readonly ITranslationPipelineCoordinator _translationPipelineCoordinator;
        private readonly IChatWindowsEventCoordinator _chatWindowsEventCoordinator;
        private readonly ISettingsMigrationService _settingsMigrationService;
        private readonly ISettingsSyncService _settingsSyncService;
        private readonly IAppLogger _logger;

        public ApplicationCoordinator(
            IFFMemoryReaderService ffMemoryReader,
            ITranslationPipelineCoordinator translationPipelineCoordinator,
            IChatWindowsEventCoordinator chatWindowsEventCoordinator,
            ISettingsMigrationService settingsMigrationService,
            ISettingsSyncService settingsSyncService,
            IAppLogger logger)
        {
            _ffMemoryReader = ffMemoryReader;
            _translationPipelineCoordinator = translationPipelineCoordinator;
            _chatWindowsEventCoordinator = chatWindowsEventCoordinator;
            _settingsMigrationService = settingsMigrationService;
            _settingsSyncService = settingsSyncService;
            _logger = logger;
        }

        public async Task InitializeAsync(TataruModel tataruModel, MainWindow mainWindow, TataruUIModel uiModel,
            TataruViewModel viewModel)
        {
            // LoadLanguages and FFMemoryReader.Start are independent; do them in parallel.
            var loadLanguagesTask = Task.Run(() => tataruModel.WebTranslator.LoadLanguages());
            var startReaderTask = Task.Run(() => _ffMemoryReader.Start());

            await Task.WhenAll(loadLanguagesTask, startReaderTask).ConfigureAwait(false);

            // Pipeline depends on both; chat windows must run on the UI thread, so marshal back.
            _translationPipelineCoordinator.Start(_ffMemoryReader, tataruModel.ChatProcessor);
            _chatWindowsEventCoordinator.Start(uiModel, viewModel, tataruModel, mainWindow);
        }

        public void Stop(IChatWindowCoordinator chatWindowCoordinator)
        {
            StopBestEffort(_chatWindowsEventCoordinator.Stop, "chat windows events");
            StopBestEffort(chatWindowCoordinator.CloseAll, "chat windows");
            StopBestEffort(_translationPipelineCoordinator.Stop, "translation pipeline");
            StopBestEffort(_ffMemoryReader.Stop, "ff memory reader");

            try
            {
                using (var cancellation = new CancellationTokenSource(SettingsShutdownTimeout))
                {
                    _settingsSyncService.StopAsync(cancellation.Token).GetAwaiter().GetResult();
                }
            }
            catch (OperationCanceledException)
            {
                _logger.WriteLog("ApplicationCoordinator.Stop settings sync timed out.");
            }
            catch (Exception ex)
            {
                _logger.WriteLog("ApplicationCoordinator.Stop settings sync failed.");
                _logger.WriteLog(ex);
            }
        }

        public async Task StopAsync(IChatWindowCoordinator chatWindowCoordinator)
        {
            StopBestEffort(_chatWindowsEventCoordinator.Stop, "chat windows events");
            StopBestEffort(chatWindowCoordinator.CloseAll, "chat windows");
            StopBestEffort(_translationPipelineCoordinator.Stop, "translation pipeline");

            try
            {
                await _ffMemoryReader.StopAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger.WriteLog("ApplicationCoordinator.StopAsync ff memory reader failed.");
                _logger.WriteLog(ex);
            }

            try
            {
                using var cancellation = new CancellationTokenSource(SettingsShutdownTimeout);
                await _settingsSyncService.StopAsync(cancellation.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                _logger.WriteLog("ApplicationCoordinator.StopAsync settings sync timed out.");
            }
            catch (Exception ex)
            {
                _logger.WriteLog("ApplicationCoordinator.StopAsync settings sync failed.");
                _logger.WriteLog(ex);
            }
        }

        private void StopBestEffort(Action action, string componentName)
        {
            try
            {
                action();
            }
            catch (OperationCanceledException)
            {
                _logger.WriteLog("ApplicationCoordinator.Stop canceled for " + componentName + ".");
            }
            catch (Exception ex)
            {
                _logger.WriteLog("ApplicationCoordinator.Stop failed for " + componentName + ".");
                _logger.WriteLog(ex);
            }
        }

        public void LoadSettings(TataruUIModel uiModel, string systemSettingFileName, ChatProcessor chatProcessor,
            WebTranslator webTranslator, Func<Task> persistSettingsAsync)
        {
            var userSettings =
                _settingsMigrationService.LoadUserSettings(systemSettingFileName, chatProcessor, webTranslator);
            uiModel.SetSettings(userSettings);
            _settingsSyncService.Start(uiModel, persistSettingsAsync);
        }
    }
}