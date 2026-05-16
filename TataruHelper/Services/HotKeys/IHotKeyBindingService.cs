using FFXIVTataruHelper.WinUtils;
using System.Windows.Input;

using FFXIVTataruHelper.Compatibility.HotKeys;

namespace FFXIVTataruHelper.Services.HotKeys
{
    public interface IHotKeyBindingService
    {
        void RegisterHotKeyDown(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination, KeyEventArgs e, bool isDisposed);

        void RegisterHotKeyUp(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination, KeyEventArgs e, bool isDisposed);

        void ReRegisterGlobalHotKey(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination, bool isDisposed);
    }
}
