using FFXIVTataruHelper.ViewModel;

namespace FFXIVTataruHelper.Views.Pages;

internal interface IWindowScopedSettingsPage
{
    void BindTo(ChatWindowViewModel window);
}