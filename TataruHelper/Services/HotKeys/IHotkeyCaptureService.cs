using System.Windows.Input;

using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Services.HotKeys;

public interface IHotkeyCaptureService
{
    void RegisterHotKeyDown(ChatWindowViewModel window, TatruHotkeyType type, KeyEventArgs args);

    void RegisterHotKeyUp(ChatWindowViewModel window, TatruHotkeyType type, KeyEventArgs args);
}