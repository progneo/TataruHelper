using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class SettingsSectionItem : INotifyPropertyChanged
{
    private string _title;

    public SettingsSectionItem(SettingsSection section, string resourceKey, string fallbackTitle)
    {
        Section = section;
        ResourceKey = resourceKey;
        _title = fallbackTitle;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public SettingsSection Section { get; }

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

    public void RefreshTitle(string localizedTitle)
    {
        Title = localizedTitle;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}