using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Factories
{
    public interface IChatWindowFactory
    {
        ChatWindow Create(TataruModel tataruModel, ChatWindowViewModel chatWindowViewModel, MainWindow mainWindow);
    }
}
