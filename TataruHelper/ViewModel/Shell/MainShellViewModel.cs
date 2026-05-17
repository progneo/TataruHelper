using System.Collections.ObjectModel;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class MainShellViewModel
{
    public MainShellViewModel()
    {
        NavigationItems = new ObservableCollection<string>
        {
            "General",
            "Chat Windows",
            "Translation",
            "Appearance",
            "Hotkeys",
            "About"
        };

        SelectedNavigationItem = NavigationItems[0];
    }

    public ObservableCollection<string> NavigationItems { get; }

    public string SelectedNavigationItem { get; set; }

    public object CurrentPageView { get; set; }
}