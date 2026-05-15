using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.ViewModel;
using System;
using System.Threading.Tasks;
using Translation;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class ApplicationCoordinator : IApplicationCoordinator
    {
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

        public async Task InitializeAsync(TataruModel tataruModel, MainWindow mainWindow, TataruUIModel uiModel, TataruViewModel viewModel)
        {
            await Task.Run(() =>
            {
                tataruModel.WebTranslator.LoadLanguages();
                _ffMemoryReader.Start();

                _translationPipelineCoordinator.Start(_ffMemoryReader, tataruModel.ChatProcessor);
                _chatWindowsEventCoordinator.Start(uiModel, viewModel, tataruModel, mainWindow);
            }).ConfigureAwait(false);
        }

        public void Stop(IChatWindowCoordinator chatWindowCoordinator)
        {
            try
            {
                _chatWindowsEventCoordinator.Stop();
                chatWindowCoordinator.CloseAll();
                _translationPipelineCoordinator.Stop();
                _ffMemoryReader.Stop();
                _settingsSyncService.StopAsync().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                _logger.WriteLog("ApplicationCoordinator.Stop canceled.");
            }
            catch (Exception ex)
            {
                _logger.WriteLog("ApplicationCoordinator.Stop failed.");
                _logger.WriteLog(ex);
                throw;
            }
        }

        public void LoadSettings(TataruUIModel uiModel, string systemSettingFileName, ChatProcessor chatProcessor, WebTranslator webTranslator, Func<Task> persistSettingsAsync)
        {
            var userSettings = _settingsMigrationService.LoadUserSettings(systemSettingFileName, chatProcessor, webTranslator);
            uiModel.SetSettings(userSettings);
            _settingsSyncService.Start(uiModel, persistSettingsAsync);
        }
    }
}
