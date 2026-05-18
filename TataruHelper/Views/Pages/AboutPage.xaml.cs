using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Navigation;

using FFXIVTataruHelper.Utils;
using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Pages;

public partial class AboutPage : UserControl, IWindowScopedSettingsPage, INotifyPropertyChanged
{
    private ChatWindowViewModel _boundWindow;

    public AboutPage(SettingsShellViewModel shell)
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

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (!ExternalLinkOpener.TryOpen(e.Uri))
        {
            Trace.TraceWarning($"AboutPage: Failed to open external link: {e.Uri?.AbsoluteUri}");
        }

        e.Handled = true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}