using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class MainShellViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TataruViewModel _settingsViewModel;
    private readonly TataruUIModel _uiModel;
    private bool _disposed;

    public MainShellViewModel(TataruViewModel settingsViewModel, TataruUIModel uiModel)
    {
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _uiModel = uiModel ?? throw new ArgumentNullException(nameof(uiModel));

        _settingsViewModel.PropertyChanged += OnSettingsViewModelPropertyChanged;
        _uiModel.PropertyChanged += OnUiModelPropertyChanged;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public TataruViewModel SettingsViewModel => _settingsViewModel;

    public ChatWindowViewModel CurrentChatWindow => _settingsViewModel.CurrentChatWindow;

    public bool IsHideSettingsToTray
    {
        get => _uiModel.IsHideSettingsToTray;
        set
        {
            if (_uiModel.IsHideSettingsToTray == value)
            {
                return;
            }

            _uiModel.IsHideSettingsToTray = value;
            OnPropertyChanged();
        }
    }

    public bool IsDirectMemoryReading
    {
        get => _uiModel.IsDirecMemoryReading;
        set
        {
            if (_uiModel.IsDirecMemoryReading == value)
            {
                return;
            }

            _uiModel.IsDirecMemoryReading = value;
            OnPropertyChanged();
        }
    }

    public void RegisterHotKeyDown(TatruHotkeyType type, KeyEventArgs e)
    {
        var current = CurrentChatWindow;
        if (current != null)
        {
            current.RegisterHotKeyDown(type, e);
        }
    }

    public void RegisterHotKeyUp(TatruHotkeyType type, KeyEventArgs e)
    {
        var current = CurrentChatWindow;
        if (current != null)
        {
            current.RegisterHotKeyUp(type, e);
        }
    }

    private void OnSettingsViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TataruViewModel.SelectedTabIndex) ||
            e.PropertyName == nameof(TataruViewModel.ChatWindows) ||
            string.IsNullOrEmpty(e.PropertyName))
        {
            OnPropertyChanged(nameof(CurrentChatWindow));
        }
    }

    private void OnUiModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TataruUIModel.IsHideSettingsToTray))
        {
            OnPropertyChanged(nameof(IsHideSettingsToTray));
        }
        else if (e.PropertyName == nameof(TataruUIModel.IsDirecMemoryReading))
        {
            OnPropertyChanged(nameof(IsDirectMemoryReading));
        }
    }

    private void OnPropertyChanged([CallerMemberName] string propertyName = "")
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _settingsViewModel.PropertyChanged -= OnSettingsViewModelPropertyChanged;
        _uiModel.PropertyChanged -= OnUiModelPropertyChanged;
        _disposed = true;
    }
}