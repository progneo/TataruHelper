using System.Windows.Controls;
using System.Windows.Input;

using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Pages;

public partial class HotkeysPage : UserControl
{
    public HotkeysPage()
    {
        InitializeComponent();
    }

    private void ShowHideChatWinHotKey_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.RegisterHotKeyDown(TatruHotkeyType.ShowHideChatWindow, e);
        }

        e.Handled = true;
    }

    private void ShowHideChatWinHotKey_KeyUp(object sender, KeyEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.RegisterHotKeyUp(TatruHotkeyType.ShowHideChatWindow, e);
        }

        e.Handled = true;
    }

    private void ClickThroughHotKey_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.RegisterHotKeyDown(TatruHotkeyType.ClickThrough, e);
        }

        e.Handled = true;
    }

    private void ClickThroughHotKey_KeyUp(object sender, KeyEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.RegisterHotKeyUp(TatruHotkeyType.ClickThrough, e);
        }

        e.Handled = true;
    }

    private void ClearChatHotKey_KeyDown(object sender, KeyEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.RegisterHotKeyDown(TatruHotkeyType.ClearChat, e);
        }

        e.Handled = true;
    }

    private void ClearChatHotKey_KeyUp(object sender, KeyEventArgs e)
    {
        if (DataContext is MainShellViewModel vm)
        {
            vm.RegisterHotKeyUp(TatruHotkeyType.ClearChat, e);
        }

        e.Handled = true;
    }
}