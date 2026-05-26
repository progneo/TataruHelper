using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;

using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.WinUtils;

namespace FFXIVTataruHelper
{
    public partial class OutgoingChatWindow : Window
    {
        private readonly OutgoingChatViewModel _viewModel;
        private readonly IAppLogger _logger;
        private readonly MainWindow _mainWindow;
        private WindowResizer _windowResizer;

        public OutgoingChatWindow(OutgoingChatViewModel viewModel, IAppLogger logger, MainWindow mainWindow)
        {
            _viewModel = viewModel;
            _logger = logger;
            _mainWindow = mainWindow;
            InitializeComponent();
            DataContext = _viewModel;
            PreviewKeyDown += OnPreviewKeyDown;
            Closing += OnClosing;
            Loaded += OnLoaded;
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            PreviewKeyDown -= OnPreviewKeyDown;
            Closing -= OnClosing;
            Loaded -= OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            _windowResizer ??= new WindowResizer(this, _logger);
            MessageTextBox?.Focus();
        }

        private void OnClosing(object sender, CancelEventArgs e)
        {
            // Hide instead of close so the same instance is reusable.
            e.Cancel = true;
            Hide();
        }

        private void OnPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && (Keyboard.Modifiers & ModifierKeys.Shift) == 0)
            {
                if (_viewModel.CopyCommand.CanExecute(null))
                {
                    _viewModel.CopyCommand.Execute(null);
                    e.Handled = true;
                }
            }
            else if (e.Key == Key.Escape)
            {
                Hide();
                e.Handled = true;
            }
        }

        public void ShowAndActivate()
        {
            Show();
            if (WindowState == WindowState.Minimized)
            {
                WindowState = WindowState.Normal;
            }

            Activate();
            MessageTextBox?.Focus();
        }

        #region Resize / drag handlers

        protected virtual void DisplayResizeCursor(object sender, MouseEventArgs e)
        {
            _windowResizer?.displayResizeCursor(sender);
        }

        protected virtual void DisplayDragCursor(object sender, MouseEventArgs e)
        {
            _windowResizer?.DisplayDragCursor(sender);
        }

        protected virtual void ResetCursor(object sender, MouseEventArgs e)
        {
            _windowResizer?.resetCursor();
        }

        protected virtual void Resize(object sender, MouseButtonEventArgs e)
        {
            _windowResizer?.resizeWindow(sender);
        }

        protected virtual void Drag(object sender, MouseButtonEventArgs e)
        {
            _windowResizer?.dragWindow(sender, e);
        }

        #endregion

        #region Context menu

        private void HideThisWindow_Click(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void Settings_Click(object sender, RoutedEventArgs e)
        {
            _mainWindow?.ShowSettingsWindow();
        }

        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        #endregion
    }
}
