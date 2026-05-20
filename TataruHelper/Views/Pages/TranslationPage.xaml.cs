using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;

using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Pages;

public partial class TranslationPage : UserControl, IWindowScopedSettingsPage, INotifyPropertyChanged
{
    private ChatWindowViewModel _boundWindow;

    public TranslationPage(SettingsShellViewModel shell)
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
            if (ReferenceEquals(_boundWindow, value)) return;
            _boundWindow = value;
            OnPropertyChanged();
        }
    }

    public void BindTo(ChatWindowViewModel window)
    {
        BoundWindow = window;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}