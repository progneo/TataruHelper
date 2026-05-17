namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class SettingsSectionItem
{
    public SettingsSectionItem(SettingsSection section, string title)
    {
        Section = section;
        Title = title;
    }

    public SettingsSection Section { get; }

    public string Title { get; }
}