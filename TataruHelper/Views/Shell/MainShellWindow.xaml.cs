using System;
using System.Windows;

using FFXIVTataruHelper.ViewModel.Shell;

namespace FFXIVTataruHelper.Views.Shell;

public partial class MainShellWindow : Window
{
    public MainShellWindow(MainShellViewModel viewModel)
    {
        if (viewModel == null)
        {
            throw new ArgumentNullException(nameof(viewModel));
        }

        InitializeComponent();
        DataContext = viewModel;
    }
}