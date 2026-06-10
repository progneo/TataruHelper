using System;
using System.ComponentModel;
using System.Drawing;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Navigation;

using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.Services.Update;
using FFXIVTataruHelper.Utils;
using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.ViewModel.Shell;
using FFXIVTataruHelper.Views.Pages;
using FFXIVTataruHelper.WinUtils;

using Hardcodet.Wpf.TaskbarNotification;

using Updater;
using Updater.EventArguments;

using Wpf.Ui.Appearance;
using Wpf.Ui.Controls;

using Timer = System.Timers.Timer;

namespace FFXIVTataruHelper;

/// <summary>
/// Interaction logic for MainWindow.xaml//
/// </summary>
public partial class MainWindow : FluentWindow
{
    private readonly ITataruModelFactory _tataruModelFactory;
    private readonly IUpdateService _updater;
    private readonly IAppLogger _logger;
    private readonly IUiDispatcher _uiDispatcher;
    private readonly IHotkeyCaptureService _hotkeyCaptureService;
    private readonly TranslationCredentialsViewModel _translationCredentials;

    private readonly LogWriter _logWriter;
    private TataruModel _tataruModel;
    private TataruUIModel _tataruUiModel;
    private SettingsShellViewModel _settingsShellViewModel;

    private Timer _updaterTimer;
    private readonly LanguageWrapper _languageWrapper;
    private readonly ISettingsStore _settingsStore;
    private readonly OptimizeFootprint _optimizeFootprint;
    private readonly WinMessagesHandler _winMessagesHandler;

    private IWindowScopedSettingsPage _chatWindowsPage;
    private IWindowScopedSettingsPage _translationPage;
    private IWindowScopedSettingsPage _appearancePage;
    private IWindowScopedSettingsPage _hotkeysPage;
    private IWindowScopedSettingsPage _generalPage;
    private IWindowScopedSettingsPage _aboutPage;

    private bool _isShutdownCleanupCompleted;

    public MainWindow(
        ITataruModelFactory tataruModelFactory,
        IUpdateService updater,
        IAppLogger logger,
        IUiDispatcher uiDispatcher,
        IHotkeyCaptureService hotkeyCaptureService,
        TranslationCredentialsViewModel translationCredentials,
        ISettingsStore settingsStore,
        LogWriter logWriter,
        LanguageWrapper languageWrapper,
        OptimizeFootprint optimizeFootprint,
        WinMessagesHandler winMessagesHandler)
    {
        _logWriter = logWriter;
        _tataruModelFactory = tataruModelFactory;
        _updater = updater;
        _logger = logger;
        _uiDispatcher = uiDispatcher;
        _hotkeyCaptureService = hotkeyCaptureService;
        _translationCredentials = translationCredentials;
        _settingsStore = settingsStore;
        _languageWrapper = languageWrapper;
        _optimizeFootprint = optimizeFootprint;
        _winMessagesHandler = winMessagesHandler;

        if (!TataruSingleInstance.IsOnlyInstance)
        {
            ShutDown();
            return;
        }

        try
        {
            InitializeComponent();
            _uiDispatcher.SetWindow(this);
        }
        catch (Exception ex)
        {
            _logger.WriteLog(ex);
            return;
        }

        try
        {
            _languageWrapper.Attach(this);
        }
        catch (Exception ex)
        {
            _logger.WriteLog(ex);
        }
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        try
        {
            _logWriter.StartWriting();
            _logger.WriteLog("TataruHelper v" + Convert.ToString(Assembly.GetEntryAssembly()?.GetName().Version));
        }
        catch (Exception ex)
        {
            _logger.WriteLog(ex);
        }

        try
        {
            SystemThemeWatcher.Watch(this, WindowBackdropType.Mica, updateAccents: true);
        }
        catch (Exception ex)
        {
            _logger.WriteLog(ex);
        }

        try
        {
            _tataruModel = _tataruModelFactory.Create(this);
            await _tataruModel.InitializeComponent();
            _tataruUiModel = _tataruModel.TataruUIModel;

            InitTataruModel();

            _tataruModel.AsyncLoadSettings().Forget();
            _tataruModel.FFMemoryReader.AddExclusionWindowHandler(new WindowInteropHelper(this).Handle);

            _settingsShellViewModel = new SettingsShellViewModel(
                _tataruModel.TataruViewModel,
                _tataruUiModel,
                _hotkeyCaptureService,
                CheckUpdates,
                _translationCredentials);

            _settingsShellViewModel.PropertyChanged += OnSettingsShellPropertyChanged;
            _settingsShellViewModel.FfStatusText = (string)Resources["FFStatusText"];

            DataContext = _settingsShellViewModel;

            InitializeSectionPages();
            BindWindowScopedPages();
            UpdateSectionContent();

            _tataruModel.TataruViewModel.ShutdownRequested += OnShutDownRequsted;

            _optimizeFootprint.Start();

            _winMessagesHandler.Attach(this);
            _winMessagesHandler.ShowFirstInstance += OnShowFirstInstance;

            _updater?.UpdateStateChanged += OnUpdaterEvent;

#if DEBUG
#else
            Task.Run(() =>
            {
                if (_updater == null)
                {
                    return;
                }

                _updater.CheckAndInstallUpdatesAsync(CmdArgsStatus.IsPreRelease, CancellationToken.None).Forget();

                _updaterTimer = new Timer(TimeSpan.FromMinutes(30).TotalMilliseconds);
                _updaterTimer.Elapsed += async (_, _) => await UpdateTimerHandler();
                _updaterTimer.AutoReset = true;
                _updaterTimer.Start();
            }).Forget();
#endif
        }
        catch (Exception ex)
        {
            _logger.WriteLog(ex);
        }
    }

    private void InitializeSectionPages()
    {
        _chatWindowsPage = new ChatWindowsPage(_settingsShellViewModel);
        _translationPage = new TranslationPage(_settingsShellViewModel);
        _appearancePage = new AppearancePage(_settingsShellViewModel);
        _hotkeysPage = new HotkeysPage(_settingsShellViewModel);
        _generalPage = new GeneralPage(_settingsShellViewModel);
        _aboutPage = new AboutPage(_settingsShellViewModel);
    }

    private void OnSettingsShellPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SettingsShellViewModel.CurrentChatWindow) ||
            e.PropertyName == nameof(SettingsShellViewModel.SelectedChatWindowId))
        {
            BindWindowScopedPages();
        }

        if (e.PropertyName == nameof(SettingsShellViewModel.SelectedSection) ||
            e.PropertyName == nameof(SettingsShellViewModel.SelectedSectionKey))
        {
            UpdateSectionContent();
        }
    }

    private void BindWindowScopedPages()
    {
        var current = _settingsShellViewModel?.CurrentChatWindow;

        _chatWindowsPage?.BindTo(current);
        _translationPage?.BindTo(current);
        _appearancePage?.BindTo(current);
        _hotkeysPage?.BindTo(current);
        _generalPage?.BindTo(current);
        _aboutPage?.BindTo(current);
    }

    private void UpdateSectionContent()
    {
        if (_settingsShellViewModel == null)
        {
            return;
        }

        SectionContentHost.Content = _settingsShellViewModel.SelectedSectionKey switch
        {
            SettingsSection.ChatWindows => _chatWindowsPage,
            SettingsSection.Translation => _translationPage,
            SettingsSection.Appearance => _appearancePage,
            SettingsSection.Hotkeys => _hotkeysPage,
            SettingsSection.General => _generalPage,
            SettingsSection.About => _aboutPage,
            _ => _chatWindowsPage
        };
    }

    private void Window_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        var winSize = new PointD(Width, Height);

        if (_tataruUiModel != null && _tataruUiModel.SettingsWindowSize != winSize)
        {
            _tataruUiModel.SettingsWindowSize = winSize;
        }
    }

    private async void Window_Closing(object sender, CancelEventArgs e)
    {
        if (_isShutdownCleanupCompleted)
        {
            e.Cancel = false;
            return;
        }

        e.Cancel = true;

        try
        {
            var cleanup = Task.Run(RunShutdownCleanupAsync);
            var winner = await Task.WhenAny(cleanup, Task.Delay(TimeSpan.FromSeconds(3))).ConfigureAwait(true);
            if (winner != cleanup)
            {
                _logger.WriteLog("MainWindow.Window_Closing forced shutdown after 3s timeout.");
            }
        }
        catch (Exception ex)
        {
            _logger.WriteLog("MainWindow.Window_Closing cleanup failed.");
            _logger.WriteLog(Convert.ToString(ex));
        }
        finally
        {
            _isShutdownCleanupCompleted = true;
            Application.Current?.Shutdown();
        }
    }

    private async Task RunShutdownCleanupAsync()
    {
        try
        {
            _updaterTimer?.Stop();
            _updater?.StopUpdate();

            _optimizeFootprint?.Stop();

            if (_tataruModel != null)
            {
                await _tataruModel.StopAsync().ConfigureAwait(false);

                try
                {
                    await _tataruModel.SaveSettings().ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.WriteLog("MainWindow shutdown save settings failed.");
                    _logger.WriteLog(ex);
                }
            }

            await _uiDispatcher.InvokeAsync(() =>
            {
                _settingsShellViewModel?.Dispose();
                TaskBarIcon?.Dispose();
            }).ConfigureAwait(false);

            TataruSingleInstance.Stop();

            _updater?.Dispose();

            _logWriter?.Dispose();
        }
        catch (Exception ex)
        {
            _logger.WriteLog("MainWindow.RunShutdownCleanupAsync failed.");
            _logger.WriteLog(Convert.ToString(ex));
        }
    }

    private async Task OnUiLanguageChange(IntegerValueChangeEventArgs ea)
    {
        await this.UIThreadAsync(() =>
        {
            if (ea.NewValue != ea.OldValue)
            {
                _languageWrapper.CurrentLanguage = (LanguageWrapper.Languages)ea.NewValue;
            }
        });
    }

    private async Task OnSettingsWindowSizeChange(PointDValueChangeEventArgs ea)
    {
        await this.UIThreadAsync(() =>
        {
            var winSize = new PointD(Width, Height);
            var uiModel = (TataruUIModel)ea.Sender;

            if (uiModel.IsFirstTime == 0)
            {
                uiModel.IsFirstTime = -1;
            }

            if (ea.NewValue == winSize)
            {
                return;
            }

            if (ea.NewValue.X > 1 && ea.NewValue.Y > 1)
            {
                Width = ea.NewValue.X;
                Height = ea.NewValue.Y;
            }
            else
            {
                uiModel.SettingsWindowSize = winSize;
            }
        });
    }

    private async Task OnFFWindowStateChange(WindowStateChangeEventArgs ea)
    {
        await this.UIThreadAsync(() =>
        {
            if (ea.IsRunningNew == ea.IsRunningOld)
            {
                return;
            }

            _settingsShellViewModel.FfStatusText = ea.IsRunningNew
                ? ((string)Resources["FFStatusTextFound"]) + " " + ea.Text
                : (string)Resources["FFStatusText"];
        });
    }

    private async Task OnShowFirstInstance(BooleanChangeEventArgs ea)
    {
        await this.UIThreadAsync(ShowSettingsWindow);
    }

    private async Task OnUpdaterEvent(UpdateStateChangedEventArgs ea)
    {
        var stateTransition = UpdateUiStateMapper.Map(
            ea.State,
            _tataruModel.TataruViewModel.UpdateCheckByUser,
            _tataruModel.TataruViewModel.RestartReadyVisibility,
            _tataruModel.TataruViewModel.DownloadingUpdateVisibility);

        // Check Updates button now lives on About page; enable/disable plumbing
        // would need a shared bindable property — deferred.
        _ = stateTransition.DisableCheckButton;

        if (stateTransition.ShowDownloading ||
            stateTransition.ShowRestartReady ||
            stateTransition.HideUserStartedText ||
            stateTransition.HideDownloading ||
            stateTransition.ShowNoUpdatesByUserRequest ||
            stateTransition.ShowErrorByUserRequest ||
            stateTransition.CompleteUserFlow)
        {
            await this.UIThreadAsync(() =>
            {
                if (stateTransition.HideUserStartedText)
                {
                    _tataruModel.TataruViewModel.UserStartedUpdateTextVisibility = false;
                }

                if (stateTransition.ShowDownloading)
                {
                    _tataruModel.TataruViewModel.DownloadingUpdateVisibility = true;
                }

                if (stateTransition.ShowRestartReady)
                {
                    _tataruModel.TataruViewModel.RestartReadyVisibility = true;
                    TaskBarIcon.ShowBalloonTip((string)Resources["NotifyUpdateTitle"],
                        (string)Resources["NotifyUpdateText"],
                        BalloonIcon.Info);
                }

                if (stateTransition.HideDownloading)
                {
                    _tataruModel.TataruViewModel.DownloadingUpdateVisibility = false;
                }

                if (stateTransition.ShowNoUpdatesByUserRequest)
                {
                    UserStartedUpdateText.Text = (string)Resources["NoUpdatesFound"];
                    _tataruModel.TataruViewModel.UserStartedUpdateTextVisibility = true;
                }

                if (stateTransition.ShowErrorByUserRequest)
                {
                    UserStartedUpdateText.Text = "Update check failed.";
                    _tataruModel.TataruViewModel.UserStartedUpdateTextVisibility = true;
                }

                if (stateTransition.CompleteUserFlow)
                {
                    _tataruModel.TataruViewModel.UpdateCheckByUser = false;
                    OnUserStartedUpdateEnd();
                }
            });
        }
    }

    private void OnUserStartedUpdateEnd()
    {
        Task.Run(async () =>
        {
            await Task.Delay((int)TimeSpan.FromSeconds(10).TotalMilliseconds);
            await this.UIThreadAsync(() =>
            {
                _tataruModel.TataruViewModel.UserStartedUpdateTextVisibility = false;
            });
        });
    }

    private void InitTataruModel()
    {
        var uiModel = _tataruModel.TataruUIModel;

        uiModel.UiLanguageChanged += OnUiLanguageChange;
        uiModel.SettingsWindowSizeChanged += OnSettingsWindowSizeChange;

        _tataruModel.FFMemoryReader.FFWindowStateChanged += OnFFWindowStateChange;
    }

    private void RestartApp_Click(object sender, MouseButtonEventArgs e)
    {
        _updater?.RestartApp();
    }

    private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
    {
        if (!ExternalLinkOpener.TryOpen(e.Uri))
        {
            _logger.WriteLog($"Failed to open external link: {e.Uri?.AbsoluteUri}");
        }

        e.Handled = true;
    }

    private void TBMenuExit_Click(object sender, RoutedEventArgs e)
    {
        ShutDown();
    }

    private void TBDoubleClick(object sender, RoutedEventArgs e)
    {
        ShowSettingsWindow();
    }

    public void ShowSettingsWindow()
    {
        Helper.Unminimize(this);

        Visibility = Visibility.Visible;
        Activate();
        Focus();
    }

    private void OnShutDownRequsted(object sender, EventArgs e)
    {
        ShutDown();
    }

    private void CheckUpdates()
    {
        if (_updater == null || _updater is DisabledUpdateService)
        {
            UserStartedUpdateText.Text = "Updater is unavailable in this build.";
            _tataruModel.TataruViewModel.UserStartedUpdateTextVisibility = true;
            return;
        }

        UserStartedUpdateText.Text = (string)Resources["LookingForUpdates"];

        _tataruModel.TataruViewModel.UpdateCheckByUser = true;
        _tataruModel.TataruViewModel.UserStartedUpdateTextVisibility = true;

        _updater.CheckAndInstallUpdatesAsync(CmdArgsStatus.IsPreRelease, CancellationToken.None).Forget();
    }

    private async Task UpdateTimerHandler()
    {
        // Invoked from an async-void timer handler, so exceptions must not escape.
        try
        {
            await Task.Run(() =>
            {
                _updater?.CheckAndInstallUpdatesAsync(CmdArgsStatus.IsPreRelease, CancellationToken.None).Forget();
            });
        }
        catch (Exception ex)
        {
            _logger.WriteLog(ex);
        }
    }

    protected override void OnStateChanged(EventArgs e)
    {
        if (WindowState == WindowState.Minimized && _settingsShellViewModel != null)
        {
            if (_settingsShellViewModel.IsHideSettingsToTray)
            {
                Hide();
            }
        }

        base.OnStateChanged(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        e.Cancel = false;
        base.OnClosing(e);
    }

    private void Window_Closed(object sender, EventArgs e)
    {
        _logWriter?.Dispose();
    }

    public void ShutDown()
    {
        Application.Current.Shutdown();
    }
}