using System.ComponentModel;
using System.Runtime.CompilerServices;

using Wpf.Ui.Controls;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class SettingsSectionItem : INotifyPropertyChanged
{
    private string _title;

    public SettingsSectionItem(
        SettingsSection section,
        string groupName,
        string resourceKey,
        string fallbackTitle,
        SymbolRegular icon)
    {
        Section = section;
        GroupName = groupName;
        ResourceKey = resourceKey;
        _title = fallbackTitle;
        Icon = icon;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public SettingsSection Section { get; }

    public string GroupName { get; }

    public string ResourceKey { get; }

    public SymbolRegular Icon { get; }

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