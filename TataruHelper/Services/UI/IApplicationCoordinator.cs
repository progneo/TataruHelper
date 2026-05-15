using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.ViewModel;
using System;
using System.Threading.Tasks;

namespace FFXIVTataruHelper.Services.UI
{
    public interface IApplicationCoordinator
    {
        Task InitializeAsync(TataruModel tataruModel, MainWindow mainWindow, TataruUIModel uiModel, TataruViewModel viewModel);

        void Stop(IChatWindowCoordinator chatWindowCoordinator);

        void LoadSettings(TataruUIModel uiModel, string systemSettingFileName, ChatProcessor chatProcessor, Translation.WebTranslator webTranslator, Func<Task> persistSettingsAsync);
    }
}
