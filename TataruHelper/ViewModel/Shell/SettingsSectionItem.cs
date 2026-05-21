using System.ComponentModel;
using System.Runtime.CompilerServices;

using Wpf.Ui.Controls;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class SettingsSectionItem : INotifyPropertyChanged
{
    private string _title;
    private string _groupName;

    public SettingsSectionItem(
        SettingsSection section,
        string groupResourceKey,
        string groupFallback,
        string resourceKey,
        string fallbackTitle,
        SymbolRegular icon)
    {
        Section = section;
        GroupResourceKey = groupResourceKey;
        _groupName = groupFallback;
        ResourceKey = resourceKey;
        _title = fallbackTitle;
        Icon = icon;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public SettingsSection Section { get; }

    public string GroupResourceKey { get; }

    public string GroupName
    {
        get => _groupName;
        private set
        {
            if (_groupName == value)
            {
                return;
            }

            _groupName = value;
            OnPropertyChanged();
        }
    }

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

    public void RefreshGroupName(string localizedGroupName)
    {
        GroupName = localizedGroupName;
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}