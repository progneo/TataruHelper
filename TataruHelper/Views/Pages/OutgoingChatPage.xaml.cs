using System.Windows.Controls;

using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Pages;

public partial class OutgoingChatPage : UserControl, IWindowScopedSettingsPage
{
    public OutgoingChatPage(SettingsShellViewModel shell)
    {
        Shell = shell;
        InitializeComponent();
        DataContext = this;
    }

    public SettingsShellViewModel Shell { get; }

    public void BindTo(ChatWindowViewModel window)
    {
        // Outgoing chat is global; nothing to rebind per chat window.
    }
}