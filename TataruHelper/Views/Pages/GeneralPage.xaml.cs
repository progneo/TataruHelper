using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Controls;
using System.Windows.Input;

using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Pages;

public partial class GeneralPage : UserControl, IWindowScopedSettingsPage, INotifyPropertyChanged
{
    private ChatWindowViewModel _boundWindow;

    public GeneralPage(SettingsShellViewModel shell)
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

    private void CredentialTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || sender is not TextBox textBox)
        {
            return;
        }

        textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
        var request = new TraversalRequest(FocusNavigationDirection.Next);
        textBox.MoveFocus(request);
        e.Handled = true;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}