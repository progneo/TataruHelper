using System.Windows.Input;

using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Services.HotKeys;

public sealed class HotkeyCaptureService : IHotkeyCaptureService
{
    public void RegisterHotKeyDown(ChatWindowViewModel window, TatruHotkeyType type, KeyEventArgs args)
    {
        if (window == null)
        {
            return;
        }

        window.RegisterHotKeyDown(type, args);
    }

    public void RegisterHotKeyUp(ChatWindowViewModel window, TatruHotkeyType type, KeyEventArgs args)
    {
        if (window == null)
        {
            return;
        }

        window.RegisterHotKeyUp(type, args);
    }
}