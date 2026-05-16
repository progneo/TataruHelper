using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Services.UI
{
    public interface IChatWindowCoordinator
    {
        void AddFromSettings(ChatWindowViewModelSettings settings, TataruViewModel viewModel);

        void RemoveFromSettings(ChatWindowViewModelSettings settings, TataruViewModel viewModel);

        void AddFromViewModel(ChatWindowViewModel viewModelWindow, TataruUIModel uiModel);

        void RemoveFromViewModel(ChatWindowViewModel viewModelWindow, TataruUIModel uiModel);

        void ShowChatWindow(TataruModel tataruModel, ChatWindowViewModel viewModelWindow, MainWindow mainWindow);

        void CloseAll();
    }
}
