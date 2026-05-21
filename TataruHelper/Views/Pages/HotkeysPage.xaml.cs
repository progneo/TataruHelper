using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Pages;

public partial class HotkeysPage : UserControl, IWindowScopedSettingsPage, INotifyPropertyChanged
{
    private ChatWindowViewModel _boundWindow;

    public HotkeysPage(SettingsShellViewModel shell)
    {
        Shell = shell;
        InitializeComponent();
        DataContext = this;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public SettingsShellViewModel Shell { get; }

    public ChatWindowViewModel BoundWindow
    {
        get => _boundWindow;
        private set
        {
            if (ReferenceEquals(_boundWindow, value))
            {
                return;
            }

            _boundWindow = value;
            OnPropertyChanged();
        }
    }

    public void BindTo(ChatWindowViewModel window)
    {
        BoundWindow = window;
    }

    private void ShowHideChatWinHotKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Shell.RegisterHotKeyDown(TatruHotkeyType.ShowHideChatWindow, e);
        e.Handled = true;
    }

    private void ShowHideChatWinHotKey_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        Shell.RegisterHotKeyUp(TatruHotkeyType.ShowHideChatWindow, e);
        e.Handled = true;
    }

    private void ClickThroughHotKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Shell.RegisterHotKeyDown(TatruHotkeyType.ClickThrough, e);
        e.Handled = true;
    }

    private void ClickThroughHotKey_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        Shell.RegisterHotKeyUp(TatruHotkeyType.ClickThrough, e);
        e.Handled = true;
    }

    private void ClearChatHotKey_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        Shell.RegisterHotKeyDown(TatruHotkeyType.ClearChat, e);
        e.Handled = true;
    }

    private void ClearChatHotKey_PreviewKeyUp(object sender, KeyEventArgs e)
    {
        Shell.RegisterHotKeyUp(TatruHotkeyType.ClearChat, e);
        e.Handled = true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}