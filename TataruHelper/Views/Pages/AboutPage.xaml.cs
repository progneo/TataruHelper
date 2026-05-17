using System.Diagnostics;
using System.Windows.Controls;
using System.Windows.Navigation;

using FFXIVTataruHelper.Utils;

namespace FFXIVTataruHelper.Views.Pages;

public partial class AboutPage : UserControl
{
    public AboutPage()
    {
        InitializeComponent();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (!ExternalLinkOpener.TryOpen(e.Uri))
        {
            Trace.TraceWarning($"AboutPage: Failed to open external link: {e.Uri?.AbsoluteUri}");
        }

        e.Handled = true;
    }
}