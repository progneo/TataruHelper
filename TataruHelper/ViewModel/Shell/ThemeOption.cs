using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;

using FFXIVTataruHelper.Theme;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class ThemeOption : INotifyPropertyChanged
{
    private string _title;

    public ThemeOption(AppThemeMode mode, string resourceKey, string fallbackTitle)
    {
        Mode = mode;
        ResourceKey = resourceKey;
        _title = fallbackTitle;
        RefreshTitleFromResources();
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public AppThemeMode Mode { get; }

    public string ResourceKey { get; }

    public string Title
    {
        get => _title;
        private set
        {
            if (_title == value)
            {
                return;
            }

            _title = value;
            OnPropertyChanged();
        }
    }

    public void RefreshTitleFromResources()
    {
        if (Application.Current?.Resources[ResourceKey] is string localized && !string.IsNullOrWhiteSpace(localized))
        {
            Title = localized;
        }
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}