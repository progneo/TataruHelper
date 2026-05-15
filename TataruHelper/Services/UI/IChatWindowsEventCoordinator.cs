using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Services.UI
{
    public interface IChatWindowsEventCoordinator
    {
        void Start(TataruUIModel uiModel, TataruViewModel viewModel, TataruModel tataruModel, MainWindow mainWindow);

        void Stop();
    }
}
