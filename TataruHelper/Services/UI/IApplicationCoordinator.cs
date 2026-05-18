using System;
using System.Threading.Tasks;

using FFXIVTataruHelper.ViewModel;

using Translation;

namespace FFXIVTataruHelper.Services.UI
{
    public interface IApplicationCoordinator
    {
        Task InitializeAsync(TataruModel tataruModel, MainWindow mainWindow, TataruUIModel uiModel,
            TataruViewModel viewModel);

        void Stop(IChatWindowCoordinator chatWindowCoordinator);

        Task StopAsync(IChatWindowCoordinator chatWindowCoordinator);

        void LoadSettings(TataruUIModel uiModel, string systemSettingFileName, ChatProcessor chatProcessor,
            WebTranslator webTranslator, Func<Task> persistSettingsAsync);
    }
}