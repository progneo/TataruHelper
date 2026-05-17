using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Input;

using FFXIVTataruHelper.Services.HotKeys;

namespace FFXIVTataruHelper.ViewModel.Shell;

public sealed class SettingsShellViewModel : INotifyPropertyChanged, IDisposable
{
    private readonly TataruViewModel _settingsViewModel;
    private readonly TataruUIModel _uiModel;
    private readonly IHotkeyCaptureService _hotkeyCaptureService;
    private readonly Action _checkUpdatesAction;
    private bool _disposed;
    private SettingsSectionItem _selectedSection;
    private LanguageOption _selectedLanguageOption;
    private string _ffStatusText;

    public SettingsShellViewModel(
        TataruViewModel settingsViewModel,
        TataruUIModel uiModel,
        IHotkeyCaptureService hotkeyCaptureService,
        Action checkUpdatesAction)
    {
        _settingsViewModel = settingsViewModel ?? throw new ArgumentNullException(nameof(settingsViewModel));
        _uiModel = uiModel ?? throw new ArgumentNullException(nameof(uiModel));
        _hotkeyCaptureService = hotkeyCaptureService ?? throw new ArgumentNullException(nameof(hotkeyCaptureService));
        _checkUpdatesAction = checkUpdatesAction ?? throw new ArgumentNullException(nameof(checkUpdatesAction));

        Sections = new ObservableCollection<SettingsSectionItem>
        {
            new(SettingsSection.ChatWindows, "ChatWindowsTab", "Chat Windows"),
            new(SettingsSection.Translation, "SectionTranslation", "Translation"),
            new(SettingsSection.Appearance, "SectionAppearance", "Appearance"),
            new(SettingsSection.Hotkeys, "ChatWindowHotkeys", "Hotkeys"),
            new(SettingsSection.General, "SectionGeneral", "General"),
            new(SettingsSection.About, "DockAbout", "About")
        };

        Languages = new ObservableCollection<LanguageOption>
        {
            new("English", "English"),
            new("Russian", "Русский"),
            new("Spanish", "Español"),
            new("Polish", "Polski"),
            new("Korean", "한국어"),
            new("PortugueseBR", "Português brasileiro"),
            new("Catalan", "Català"),
            new("Italian", "Italiano"),
            new("Japanese", "日本語"),
            new("Ukrainian", "Українська"),
            new("Chinese", "汉语"),
            new("ChineseTR", "繁體中文")
        };

        SwitchLanguageCommand = new TataruUICommand(ExecuteSwitchLanguage);
        CheckUpdatesCommand = new TataruUICommand(() => _checkUpdatesAction());
        SelectChatWindowCommand = new TataruUICommand(SelectChatWindowByParameter);
        AddWindowCommand = new TataruUICommand(() => _settingsViewModel.AddNewChatWindowCommand.Execute(null));
        DeleteWindowCommand = new TataruUICommand(() => _settingsViewModel.DeleteChatWindowCommand.Execute(null));
        ShowHideWindowCommand = new TataruUICommand(ToggleCurrentWindowVisibility);
        ResetPositionCommand = new TataruUICommand(ResetCurrentWindowPosition);

        _selectedSection = Sections.First(x => x.Section == SettingsSection.ChatWindows);
        _selectedLanguageOption = ResolveLanguageOption(_uiModel.UiLanguage);
        _ffStatusText = string.Empty;

        RefreshSectionTitles();

        _settingsViewModel.PropertyChanged += OnSettingsViewModelPropertyChanged;
        _uiModel.PropertyChanged += OnUiModelPropertyChanged;
    }

    public event PropertyChangedEventHandler PropertyChanged;

    public ObservableCollection<SettingsSectionItem> Sections { get; }

    public ObservableCollection<LanguageOption> Languages { get; }

    public TataruViewModel SettingsViewModel => _settingsViewModel;

    public SettingsSectionItem SelectedSection
    {
        get => _selectedSection;
        set
        {
            if (ReferenceEquals(_selectedSection, value) || value == null)
            {
                return;
            }

            _selectedSection = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(SelectedSectionKey));
        }
    }

    public SettingsSection SelectedSectionKey => SelectedSection.Section;

    public long SelectedChatWindowId
    {
        get => CurrentChatWindow?.WinId ?? -1;
        set
        {
            var index = _settingsViewModel.ChatWindows.ToList().FindIndex(x => x.WinId == value);
            if (index >= 0 && _settingsViewModel.SelectedTabIndex != index)
            {
                _settingsViewModel.SelectedTabIndex = index;
                OnPropertyChanged();
                OnPropertyChanged(nameof(CurrentChatWindow));
            }
        }
    }

    public ChatWindowViewModel CurrentChatWindow => _settingsViewModel.CurrentChatWindow;

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

    public string FfStatusText
    {
        get => _ffStatusText;
        set
        {
            if (string.Equals(_ffStatusText, value, StringComparison.Ordinal))
            {
                return;
            }

            _ffStatusText = value;
            OnPropertyChanged();
        }
    }

    public LanguageOption SelectedLanguageOption
    {
        get => _selectedLanguageOption;
        set
        {
            if (value == null || ReferenceEquals(_selectedLanguageOption, value))
            {
                return;
            }

            _selectedLanguageOption = value;
            OnPropertyChanged();
            SwitchLanguageCommand.Execute(value.Value);
        }
    }

    public TataruUICommand SwitchLanguageCommand { get; }

    public TataruUICommand CheckUpdatesCommand { get; }

    public TataruUICommand SelectChatWindowCommand { get; }

    public TataruUICommand AddWindowCommand { get; }

    public TataruUICommand DeleteWindowCommand { get; }

    public TataruUICommand ShowHideWindowCommand { get; }

    public TataruUICommand ResetPositionCommand { get; }

    public void RegisterHotKeyDown(TatruHotkeyType type, KeyEventArgs args)
    {
        _hotkeyCaptureService.RegisterHotKeyDown(CurrentChatWindow, type, args);
    }

    public void RegisterHotKeyUp(TatruHotkeyType type, KeyEventArgs args)
    {
        _hotkeyCaptureService.RegisterHotKeyUp(CurrentChatWindow, type, args);
    }

    private void ExecuteSwitchLanguage(object parameter)
    {
        _settingsViewModel.SwitchLanguageCommand.Execute(parameter);
    }

    private void SelectChatWindowByParameter(object parameter)
    {
        if (parameter == null)
        {
            return;
        }

        if (long.TryParse(parameter.ToString(), out var winId))
        {
            SelectedChatWindowId = winId;
        }
    }

    private void ToggleCurrentWindowVisibility()
    {
        if (CurrentChatWindow == null)
        {
            return;
        }

        CurrentChatWindow.ShowChatWindowCommand.Execute(null);
    }

    private void ResetCurrentWindowPosition()
    {
        if (CurrentChatWindow == null)
        {
            return;
        }

        CurrentChatWindow.RestChatWindowPositionCommand.Execute(null);
    }

    private LanguageOption ResolveLanguageOption(int languageId)
    {
        var language = (LanguagueWrapper.Languages)languageId;
        var value = language switch
        {
            LanguagueWrapper.Languages.Russian => "Russian",
            LanguagueWrapper.Languages.Spanish => "Spanish",
            LanguagueWrapper.Languages.Polish => "Polish",
            LanguagueWrapper.Languages.Korean => "Korean",
            LanguagueWrapper.Languages.PortugueseBR => "PortugueseBR",
            LanguagueWrapper.Languages.Catalan => "Catalan",
            LanguagueWrapper.Languages.Italian => "Italian",
            LanguagueWrapper.Languages.Ukrainian => "Ukrainian",
            LanguagueWrapper.Languages.Chinese => "Chinese",
            LanguagueWrapper.Languages.ChineseTR => "ChineseTR",
            LanguagueWrapper.Languages.Japanese => "Japanese",
            _ => "English"
        };

        return Languages.FirstOrDefault(x => x.Value == value) ?? Languages[0];
    }

    private void OnSettingsViewModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TataruViewModel.SelectedTabIndex) ||
            e.PropertyName == nameof(TataruViewModel.ChatWindows) ||
            e.PropertyName == nameof(TataruViewModel.CurrentChatWindow) ||
            string.IsNullOrEmpty(e.PropertyName))
        {
            OnPropertyChanged(nameof(CurrentChatWindow));
            OnPropertyChanged(nameof(SelectedChatWindowId));
        }
    }

    private void OnUiModelPropertyChanged(object sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TataruUIModel.IsHideSettingsToTray))
        {
            OnPropertyChanged(nameof(IsHideSettingsToTray));
            return;
        }

        if (e.PropertyName == nameof(TataruUIModel.IsDirecMemoryReading))
        {
            OnPropertyChanged(nameof(IsDirectMemoryReading));
            return;
        }

        if (e.PropertyName == nameof(TataruUIModel.UiLanguage))
        {
            _selectedLanguageOption = ResolveLanguageOption(_uiModel.UiLanguage);
            OnPropertyChanged(nameof(SelectedLanguageOption));
            RefreshSectionTitles();
        }
    }

    private void RefreshSectionTitles()
    {
        foreach (var section in Sections)
        {
            var resourceValue = Application.Current?.Resources?[section.ResourceKey] as string;
            if (!string.IsNullOrWhiteSpace(resourceValue))
            {
                section.RefreshTitle(resourceValue);
            }
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