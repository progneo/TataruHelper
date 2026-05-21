// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com


using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;

using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.TataruComponentModel;
using FFXIVTataruHelper.UIModel;
using FFXIVTataruHelper.WinUtils;

using Translation;

using Color = System.Windows.Media.Color;
using FontFamily = System.Windows.Media.FontFamily;

namespace FFXIVTataruHelper.ViewModel
{
    public enum TatruHotkeyType : int
    {
        ShowHideChatWindow = 0,
        ClickThrough = 1,
        ClearChat = 2
    }

    public class ChatWindowViewModel : INotifyPropertyChanged, IDisposable, INotifyPropertyChangedAsync
    {
        #region **Events.

        public event PropertyChangedEventHandler PropertyChanged;

        public event AsyncEventHandler<AsyncPropertyChangedEventArgs> AsyncPropertyChanged
        {
            add { this._asyncPropertyChanged.Register(value); }
            remove { this._asyncPropertyChanged.Unregister(value); }
        }

        private readonly AsyncEvent<AsyncPropertyChangedEventArgs> _asyncPropertyChanged;

        public event AsyncEventHandler<TatruEventArgs> RequestChatClear
        {
            add { this._requestChatClear.Register(value); }
            remove { this._requestChatClear.Unregister(value); }
        }

        private readonly AsyncEvent<TatruEventArgs> _requestChatClear;

        #endregion

        #region **Properties.

        public long WinId { get; }

        public string Name
        {
            get { return _Name; }
            set
            {
                if (_Name == value) return;

                _Name = value;

                if (_BoundSettings != null && _BoundSettings.Name != value)
                    _BoundSettings.Name = value;

                OnPropertyChanged();
            }
        }

        public double ChatFontSize
        {
            get { return _ChatFontSize; }
            set
            {
                if (_ChatFontSize == value) return;

                _ChatFontSize = value;
                OnPropertyChanged();
            }
        }

        public double LineBreakHeight
        {
            get { return _LineBreakHeight; }
            set
            {
                if (_LineBreakHeight == value) return;

                _LineBreakHeight = value;
                OnPropertyChanged();
            }
        }

        public int SpacingCount
        {
            get { return _SpacingCount; }
            set
            {
                if (_SpacingCount == value) return;

                _SpacingCount = value;
                OnPropertyChanged();
            }
        }

        public FontFamily ChatFont
        {
            get { return _ChatFont; }
            set
            {
                if (_ChatFont == value) return;

                _ChatFont = value;
                OnPropertyChanged();
            }
        }

        public bool IsAlwaysOnTop
        {
            get { return _IsAlwaysOnTop; }
            set
            {
                if (_IsAlwaysOnTop == value) return;

                _IsAlwaysOnTop = value;
                OnPropertyChanged();
            }
        }

        public bool IsClickThrough
        {
            get { return _IsClickThrough; }
            set
            {
                if (_IsClickThrough == value) return;

                _IsClickThrough = value;
                OnPropertyChanged();
            }
        }

        public bool IsAutoHide
        {
            get { return _IsAutoHide; }
            set
            {
                if (_IsAutoHide == value) return;

                _IsAutoHide = value;
                OnPropertyChanged();
            }
        }

        public TimeSpan AutoHideTimeout
        {
            get { return _AutoHideTimeout; }
            set
            {
                if (_AutoHideTimeout == value) return;

                _AutoHideTimeout = value;
                OnPropertyChanged();
                OnPropertyChanged("AutoHideTimeoutSeconds");
            }
        }

        public int AutoHideTimeoutSeconds
        {
            get { return (int)Math.Round(AutoHideTimeout.TotalSeconds); }
            set
            {
                if ((int)Math.Round(AutoHideTimeout.TotalSeconds) == value) return;

                _AutoHideTimeout = new TimeSpan(0, 0, value);
                OnPropertyChanged();
                OnPropertyChanged("AutoHideTimeout");
            }
        }

        public Color BackGroundColor
        {
            get { return _BackGroundColor; }
            set
            {
                if (_BackGroundColor == value) return;

                var tmpColor = value;
                if (tmpColor.A < 1)
                    tmpColor.A = 1;

                _BackGroundColor = tmpColor;
                OnPropertyChanged();
            }
        }

        public RectangleD ChatWindowRectangle
        {
            get { return _ChatWindowRectangle; }
            set
            {
                if (_ChatWindowRectangle == value) return;

                _ChatWindowRectangle = value;
                OnPropertyChanged();
                OnPropertyChanged("ChatWindowTop");
                OnPropertyChanged("ChatWindowLeft");
                OnPropertyChanged("ChatWindowWidth");
                OnPropertyChanged("ChatWindowHeight");
            }
        }

        public double ChatWindowTop
        {
            get { return ChatWindowRectangle.Y; }
            set
            {
                if (ChatWindowRectangle.Y == value) return;

                var rect = ChatWindowRectangle;
                rect.Y = value;
                ChatWindowRectangle = rect;
            }
        }

        public double ChatWindowLeft
        {
            get { return ChatWindowRectangle.X; }
            set
            {
                if (ChatWindowRectangle.X == value) return;

                var rect = ChatWindowRectangle;
                rect.X = value;
                ChatWindowRectangle = rect;
            }
        }

        public double ChatWindowWidth
        {
            get { return ChatWindowRectangle.Width; }
            set
            {
                if (ChatWindowRectangle.Width == value) return;

                var rect = ChatWindowRectangle;
                rect.Width = value;
                ChatWindowRectangle = rect;
            }
        }

        public double ChatWindowHeight
        {
            get { return ChatWindowRectangle.Height; }
            set
            {
                if (ChatWindowRectangle.Height == value) return;

                var rect = ChatWindowRectangle;
                rect.Height = value;
                ChatWindowRectangle = rect;
            }
        }

        public bool IsHiddenByUser
        {
            get { return _IsHiddenByUser; }
            set
            {
                if (_IsHiddenByUser == value) return;

                _IsHiddenByUser = value;
                OnPropertyChanged();
            }
        }

        public BindingList<ChatCodeViewModel> ChatCodes
        {
            get { return _ChatCodes; }
            set
            {
                if (_ChatCodes != null)
                    _ChatCodes.ListChanged -= OnChatCodesChange;

                _ChatCodes = value;

                _ChatCodes.ListChanged += OnChatCodesChange;
                OnPropertyChanged();
            }
        }

        public HotKeyCombination ShowHideChatKeys
        {
            get { return _ShowHideChatKeys; }
            set
            {
                if (_ShowHideChatKeys == value) return;

                _ShowHideChatKeys = new HotKeyCombination("ShowHideChatKeys" + Convert.ToString(WinId), value);

                ReRegisterGlobalHotekey(_HotKeyManager, ref _ShowHideChat, _ShowHideChatKeys);

                OnPropertyChanged();
            }
        }

        public string ShowHideChatKeysName
        {
            get
            {
                if (ShowHideChatKeys.IsInitialized)
                    return ShowHideChatKeys.CombinationKeysName();
                else
                    return _NotInitializedHKText;
            }
        }

        public HotKeyCombination ClickThoughtChatKeys
        {
            get { return _ClickThoughtChatKeys; }
            set
            {
                if (_ClickThoughtChatKeys == value) return;

                _ClickThoughtChatKeys = new HotKeyCombination("ClickThoughtChatKeys" + Convert.ToString(WinId), value);

                ReRegisterGlobalHotekey(_HotKeyManager, ref _ClickThoughtChat, _ClickThoughtChatKeys);

                OnPropertyChanged();
            }
        }

        public string ClickThoughtChatKeysName
        {
            get
            {
                if (ClickThoughtChatKeys.IsInitialized)
                    return ClickThoughtChatKeys.CombinationKeysName();
                else
                    return _NotInitializedHKText;
            }
        }

        public HotKeyCombination ClearChatKeys
        {
            get { return _ClearChatKeys; }
            set
            {
                if (_ClearChatKeys == value) return;

                _ClearChatKeys = new HotKeyCombination("ClearChatKeys" + Convert.ToString(WinId), value);

                ReRegisterGlobalHotekey(_HotKeyManager, ref _ClearChat, _ClearChatKeys);

                OnPropertyChanged();
            }
        }

        public string ClearChatKeysName
        {
            get
            {
                if (ClearChatKeys.IsInitialized)
                    return ClearChatKeys.CombinationKeysName();
                else
                    return _NotInitializedHKText;
            }
        }

        public ObservableCollection<TranslationEngine> AvailableEngines { get; } =
            new ObservableCollection<TranslationEngine>();

        public TranslationEngine SelectedEngine
        {
            get => _selectedEngine;
            set
            {
                if (ReferenceEquals(_selectedEngine, value)) return;
                if (value != null && !AvailableEngines.Contains(value)) return;

                _selectedEngine = value;

                if (_BoundSettings != null && value != null)
                {
                    _BoundSettings.TranslationEngineName = value.EngineName;
                }

                RebuildLanguagesForSelectedEngine();
                OnPropertyChanged();
            }
        }

        public CollectionView TranslateFromLanguagues
        {
            get { return _TranslateFromLanguagues; }
            set
            {
                if (_TranslateFromLanguagues == value) return;
                _TranslateFromLanguagues = value;
            }
        }

        public TranslatorLanguague CurrentTranslateFromLanguague
        {
            get { return (TranslatorLanguague)TranslateFromLanguagues.CurrentItem; }
        }

        public CollectionView TranslateToLanguagues
        {
            get { return _TranslateToLanguagues; }
            set
            {
                if (_TranslateToLanguagues == value) return;
                TranslateToLanguagues = value;
            }
        }

        public TranslatorLanguague CurrentTranslateToLanguague
        {
            get { return (TranslatorLanguague)TranslateToLanguagues.CurrentItem; }
        }

        public bool ShowTimestamps
        {
            get => _ShowTimestamps;
            set
            {
                if (_ShowTimestamps == value) return;

                _ShowTimestamps = value;
                OnPropertyChanged();
            }
        }

        public double WindowCornerRadius
        {
            get => _windowCornerRadius;
            set
            {
                if (_windowCornerRadius == value) return;

                _windowCornerRadius = value;
                OnPropertyChanged();
            }
        }

        public double ContentPadding
        {
            get => _contentPadding;
            set
            {
                if (_contentPadding == value) return;

                _contentPadding = value;
                OnPropertyChanged();
            }
        }

        public bool MessagesInContainer
        {
            get => _messagesInContainer;
            set
            {
                if (_messagesInContainer == value) return;

                _messagesInContainer = value;
                OnPropertyChanged();
            }
        }

        public double MessageContainerPadding
        {
            get => _messageContainerPadding;
            set
            {
                if (_messageContainerPadding == value) return;

                _messageContainerPadding = value;
                OnPropertyChanged();
            }
        }

        public int MessageContainerAlpha
        {
            get => _messageContainerAlpha;
            set
            {
                if (_messageContainerAlpha == value) return;

                _messageContainerAlpha = value;
                OnPropertyChanged();
            }
        }

        public double MessageContainerBorderThickness
        {
            get => _messageContainerBorderThickness;
            set
            {
                if (_messageContainerBorderThickness == value) return;

                _messageContainerBorderThickness = value;
                OnPropertyChanged();
            }
        }

        public int MessageContainerBorderAlpha
        {
            get => _messageContainerBorderAlpha;
            set
            {
                if (_messageContainerBorderAlpha == value) return;

                _messageContainerBorderAlpha = value;
                OnPropertyChanged();
            }
        }

        public bool ShowOnlyLastMessage
        {
            get => _showOnlyLastMessage;
            set
            {
                if (_showOnlyLastMessage == value) return;

                _showOnlyLastMessage = value;
                OnPropertyChanged();
            }
        }

        public bool IsSelected
        {
            get { return _IsSelected; }
            set
            {
                _IsSelected = value;
                OnPropertyChanged();
            }
        }

        public bool IsWindowVisible
        {
            get { return _IsWindowVisible; }
            set
            {
                if (_IsWindowVisible == value) return;

                _IsWindowVisible = value;
                OnPropertyChanged();
            }
        }

        public TataruUICommand ShowChatWindowCommand { get; set; }

        public TataruUICommand RestChatWindowPositionCommand { get; set; }

        #endregion

        #region **LocalVariables.

        private ChatWindowViewModelSettings _BoundSettings;

        string _Name;

        double _ChatFontSize;
        double _LineBreakHeight;
        int _SpacingCount;

        FontFamily _ChatFont;

        bool _IsAlwaysOnTop;
        bool _IsClickThrough;
        bool _IsAutoHide;

        TimeSpan _AutoHideTimeout;

        bool _IsHiddenByUser;

        Color _BackGroundColor;

        TranslationEngine _selectedEngine;
        IReadOnlyList<TranslationEngine> _allTranslationEngines;
        TranslationCredentialsViewModel _engineAvailability;
        CollectionView _TranslateFromLanguagues;
        CollectionView _TranslateToLanguagues;

        RectangleD _ChatWindowRectangle;

        BindingList<ChatCodeViewModel> _ChatCodes;

        HotKeyCombination _ShowHideChatKeys;
        HotKeyCombination _ClickThoughtChatKeys;
        HotKeyCombination _ClearChatKeys;

        GlobalHotKey _ShowHideChat;
        GlobalHotKey _ClickThoughtChat;
        GlobalHotKey _ClearChat;

        HotKeyManager _HotKeyManager;

        bool _ShowTimestamps;
        double _windowCornerRadius = 12;
        double _contentPadding = 12;
        bool _messagesInContainer;
        double _messageContainerPadding = 6;
        int _messageContainerAlpha = 32;
        double _messageContainerBorderThickness;
        int _messageContainerBorderAlpha = 96;
        bool _showOnlyLastMessage;

        bool _IsSelected;

        bool _IsWindowVisible;

        string _NotInitializedHKText = "Empty";

        bool _disposed = false;
        readonly IAppLogger _Logger;
        readonly IHotKeyBindingService _HotKeyBindingService;

        #endregion

        public ChatWindowViewModel()
        {
            _Logger = new AppLogger();
            _HotKeyBindingService = new HotKeyBindingService(_Logger);
            this._asyncPropertyChanged = new AsyncEvent<AsyncPropertyChangedEventArgs>(this.EventErrorHandler,
                "ChatWindowViewModel \n AsyncPropertyChanged");
            ShowChatWindowCommand = new TataruUICommand(ShowChatWindow);
            RestChatWindowPositionCommand = new TataruUICommand(ResetChatWindowPosition);

            Random rnd = new Random(DateTime.UtcNow.Date.Millisecond);
            WinId = (DateTime.Now.Ticks * 100u) + ((uint)rnd.Next(0, 100));

            Name = "2";

            ChatFontSize = 1;
            LineBreakHeight = 1;
            SpacingCount = 1;
            BackGroundColor = Color.FromArgb(255, 0, 255, 128);
            ChatWindowRectangle = new RectangleD(0, 0, 480, 320);
            ContentPadding = 12;
            MessagesInContainer = false;
            MessageContainerPadding = 6;
            MessageContainerAlpha = 32;
            MessageContainerBorderThickness = 0;
            MessageContainerBorderAlpha = 96;
            ShowOnlyLastMessage = false;

            ChatCodes = new BindingList<ChatCodeViewModel>()
            {
                new ChatCodeViewModel("0039", "System", Colors.Aqua, true),
            };
        }

        public ChatWindowViewModel(
            ChatWindowViewModelSettings settings,
            List<TranslationEngine> translationEngines,
            TranslationCredentialsViewModel engineAvailability,
            List<ChatMsgType> allChatCodes,
            HotKeyManager hotKeyManager,
            IAppLogger logger,
            IHotKeyBindingService hotKeyBindingService)
        {
            _Logger = logger;
            _HotKeyBindingService = hotKeyBindingService;
            this._asyncPropertyChanged = new AsyncEvent<AsyncPropertyChangedEventArgs>(this.EventErrorHandler,
                "ChatWindowViewModel \n AsyncPropertyChanged");
            this._requestChatClear =
                new AsyncEvent<TatruEventArgs>(this.EventErrorHandler, "ChatWindowViewModel \n RequsetChatClear");
            ShowChatWindowCommand = new TataruUICommand(ShowChatWindow);
            RestChatWindowPositionCommand = new TataruUICommand(ResetChatWindowPosition);

            _IsWindowVisible = true;

            _BoundSettings = settings;

            Name = settings.Name;
            WinId = settings.WinId;

            ChatFontSize = settings.ChatFontSize;
            LineBreakHeight = settings.LineBreakHeight;
            SpacingCount = settings.SpacingCount;

            ChatFont = settings.ChatFont;

            IsAlwaysOnTop = settings.IsAlwaysOnTop;
            IsClickThrough = settings.IsClickThrough;
            IsAutoHide = settings.IsAutoHide;

            AutoHideTimeout = settings.AutoHideTimeout;

            IsHiddenByUser = false;

            ShowTimestamps = settings.ShowTimestamps;

            WindowCornerRadius = settings.WindowCornerRadius > 0 ? settings.WindowCornerRadius : 12;
            ContentPadding = settings.ContentPadding;
            MessagesInContainer = settings.MessagesInContainer;
            MessageContainerPadding = settings.MessageContainerPadding;
            MessageContainerAlpha = settings.MessageContainerAlpha;
            MessageContainerBorderThickness = settings.MessageContainerBorderThickness;
            MessageContainerBorderAlpha = settings.MessageContainerBorderAlpha;
            ShowOnlyLastMessage = settings.ShowOnlyLastMessage;

            BackGroundColor = settings.BackGroundColor;

            _allTranslationEngines = translationEngines;
            _engineAvailability = engineAvailability;

            var savedEngineName = settings.TranslationEngineName;

            RebuildAvailableEngines();

            if (_engineAvailability != null)
            {
                _engineAvailability.AvailableEnginesChanged += OnEngineAvailabilityChanged;
            }

            var savedEngine = _allTranslationEngines
                .FirstOrDefault(x => x.EngineName == savedEngineName);

            SelectedEngine = AvailableEngines.Contains(savedEngine)
                ? savedEngine
                : AvailableEngines.FirstOrDefault();

            TrySetLangugue(_TranslateFromLanguagues, settings.FromLanguague);
            TrySetLangugue(_TranslateToLanguagues, settings.ToLanguague);

            ChatWindowRectangle = settings.ChatWindowRectangle;

            ChatCodes = LoadChatCodes(settings.ChatCodes, allChatCodes);

            _HotKeyManager = hotKeyManager;

            ShowHideChatKeys = new HotKeyCombination(settings.ShowHideChatKeys);
            ClickThoughtChatKeys = new HotKeyCombination(settings.ClickThoughtChatKeys);
            ClearChatKeys = new HotKeyCombination(settings.ClearChatKeys);

            _HotKeyManager.GlobalHotKeyPressed += OnGlobalHotKeyPressed;
        }

        public ChatWindowViewModelSettings GetSettings()
        {
            ChatWindowViewModelSettings settings = new ChatWindowViewModelSettings();

            settings.Name = this.Name;
            settings.WinId = this.WinId;

            settings.ChatFontSize = this.ChatFontSize;
            settings.LineBreakHeight = this.LineBreakHeight;
            settings.SpacingCount = this.SpacingCount;

            settings.IsAlwaysOnTop = this.IsAlwaysOnTop;
            settings.IsClickThrough = this.IsClickThrough;
            settings.IsAutoHide = this.IsAutoHide;

            settings.AutoHideTimeout = this.AutoHideTimeout;

            //settings.IsHiddenByUser = this.IsHiddenByUser;

            settings.BackGroundColor = this.BackGroundColor;

            settings.WindowCornerRadius = this.WindowCornerRadius;
            settings.ShowTimestamps = this.ShowTimestamps;
            settings.ContentPadding = this.ContentPadding;
            settings.MessagesInContainer = this.MessagesInContainer;
            settings.MessageContainerPadding = this.MessageContainerPadding;
            settings.MessageContainerAlpha = this.MessageContainerAlpha;
            settings.MessageContainerBorderThickness = this.MessageContainerBorderThickness;
            settings.MessageContainerBorderAlpha = this.MessageContainerBorderAlpha;
            settings.ShowOnlyLastMessage = this.ShowOnlyLastMessage;

            settings.TranslationEngineName = SelectedEngine?.EngineName ?? TranslationEngineName.GoogleTranslate;

            settings.FromLanguague = (TranslatorLanguague)TranslateFromLanguagues.CurrentItem;
            settings.ToLanguague = (TranslatorLanguague)TranslateToLanguagues.CurrentItem;

            settings.ChatWindowRectangle = this.ChatWindowRectangle;

            settings.ChatCodes = this.ChatCodes.ToList().Select(entry => new ChatCodeViewModel(entry)).ToList();

            settings.ShowHideChatKeys = new HotKeyCombination(this.ShowHideChatKeys);
            settings.ClickThoughtChatKeys = new HotKeyCombination(this.ClickThoughtChatKeys);
            settings.ClearChatKeys = new HotKeyCombination(this.ClearChatKeys);

            return settings;
        }

        public void RegisterHotKeyDown(TatruHotkeyType type, KeyEventArgs e)
        {
            switch (type)
            {
                case TatruHotkeyType.ShowHideChatWindow:
                    RegisterHotKeyDown(ref _ShowHideChat, _ShowHideChatKeys, e);
                    OnPropertyChanged("ShowHideChatKeys");
                    OnPropertyChanged("ShowHideChatKeysName");
                    break;
                case TatruHotkeyType.ClickThrough:
                    RegisterHotKeyDown(ref _ClickThoughtChat, _ClickThoughtChatKeys, e);
                    OnPropertyChanged("ClickThoughtChatKeys");
                    OnPropertyChanged("ClickThoughtChatKeysName");
                    break;
                case TatruHotkeyType.ClearChat:
                    RegisterHotKeyDown(ref _ClearChat, _ClearChatKeys, e);
                    OnPropertyChanged("ClearChatKeys");
                    OnPropertyChanged("ClearChatKeysName");
                    break;
            }
        }

        public void RegisterHotKeyUp(TatruHotkeyType type, KeyEventArgs e)
        {
            switch (type)
            {
                case TatruHotkeyType.ShowHideChatWindow:
                    RegisterHotKeyUp(ref _ShowHideChat, _ShowHideChatKeys, e);
                    OnPropertyChanged("ShowHideChatKeys");
                    OnPropertyChanged("ShowHideChatKeysName");
                    break;
                case TatruHotkeyType.ClickThrough:
                    RegisterHotKeyUp(ref _ClickThoughtChat, _ClickThoughtChatKeys, e);
                    OnPropertyChanged("ClickThoughtChatKeys");
                    OnPropertyChanged("ClickThoughtChatKeysName");
                    break;
                case TatruHotkeyType.ClearChat:
                    RegisterHotKeyUp(ref _ClearChat, _ClearChatKeys, e);
                    OnPropertyChanged("ClearChatKeys");
                    OnPropertyChanged("ClearChatKeysName");
                    break;
            }
        }

        private void RegisterHotKeyDown(ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination,
            KeyEventArgs e)
        {
            _HotKeyBindingService.RegisterHotKeyDown(_HotKeyManager, ref globalHotKey, hotKeyCombination, e, _disposed);
        }

        private void RegisterHotKeyUp(ref GlobalHotKey globalHotKey, HotKeyCombination hotKeyCombination,
            KeyEventArgs e)
        {
            _HotKeyBindingService.RegisterHotKeyUp(_HotKeyManager, ref globalHotKey, hotKeyCombination, e, _disposed);
        }

        private void ReRegisterGlobalHotekey(HotKeyManager hotKeyManager, ref GlobalHotKey globalHotKey,
            HotKeyCombination hotKeyCombination)
        {
            _HotKeyBindingService.ReRegisterGlobalHotKey(hotKeyManager, ref globalHotKey, hotKeyCombination, _disposed);
        }

        private void RebuildAvailableEngines()
        {
            TranslatorLanguague prevFrom = null, prevTo = null;
            if (_TranslateFromLanguagues != null) prevFrom = (TranslatorLanguague)_TranslateFromLanguagues.CurrentItem;
            if (_TranslateToLanguagues != null) prevTo = (TranslatorLanguague)_TranslateToLanguagues.CurrentItem;

            AvailableEngines.Clear();
            if (_allTranslationEngines == null) return;

            var enabledNames = _engineAvailability?.AvailableEngines;
            foreach (var engine in _allTranslationEngines)
            {
                if (enabledNames == null || enabledNames.Contains(engine.EngineName))
                {
                    AvailableEngines.Add(engine);
                }
            }

            if (_selectedEngine != null && !AvailableEngines.Contains(_selectedEngine))
            {
                SelectedEngine = AvailableEngines.FirstOrDefault();
                return;
            }

            if (_selectedEngine != null && _TranslateFromLanguagues != null)
            {
                TrySetLangugue(_TranslateFromLanguagues, prevFrom);
                TrySetLangugue(_TranslateToLanguagues, prevTo);
            }
        }

        private void OnEngineAvailabilityChanged(object sender, EventArgs e)
        {
            RebuildAvailableEngines();
        }

        private void RebuildLanguagesForSelectedEngine()
        {
            TranslatorLanguague prevFrom = null, prevTo = null;
            if (_TranslateFromLanguagues != null) prevFrom = (TranslatorLanguague)_TranslateFromLanguagues.CurrentItem;
            if (_TranslateToLanguagues != null) prevTo = (TranslatorLanguague)_TranslateToLanguagues.CurrentItem;

            if (_TranslateFromLanguagues != null)
                _TranslateFromLanguagues.CurrentChanged -= OnTranslateFromLanguageChange;
            if (_TranslateToLanguagues != null)
                _TranslateToLanguagues.CurrentChanged -= OnTranslateToLanguageChange;

            if (_selectedEngine == null)
            {
                _TranslateFromLanguagues = null;
                _TranslateToLanguagues = null;
                OnPropertyChanged("TranslateFromLanguagues");
                OnPropertyChanged("TranslateToLanguagues");
                return;
            }

            _TranslateFromLanguagues = new CollectionView(_selectedEngine.SupportedLanguages.ToList());
            _TranslateFromLanguagues.CurrentChanged += OnTranslateFromLanguageChange;
            OnPropertyChanged("TranslateFromLanguagues");

            var supportedToLanguages = _selectedEngine.SupportedLanguages.ToList();
            var auto = supportedToLanguages.FirstOrDefault(x => x.SystemName == "Auto");
            if (auto != null) supportedToLanguages.Remove(auto);

            _TranslateToLanguagues = new CollectionView(supportedToLanguages);
            _TranslateToLanguagues.CurrentChanged += OnTranslateToLanguageChange;
            OnPropertyChanged("TranslateToLanguagues");

            if (prevFrom != null && prevTo != null)
            {
                TrySetLangugue(_TranslateFromLanguagues, prevFrom);
                TrySetLangugue(_TranslateToLanguagues, prevTo);
            }
        }

        private void OnTranslateFromLanguageChange(object sender, EventArgs e)
        {
            OnPropertyChanged("TranslateFromLanguagues");
        }

        private void OnTranslateToLanguageChange(object sender, EventArgs e)
        {
            OnPropertyChanged("TranslateToLanguagues");
        }

        private void OnChatCodesChange(object sender, ListChangedEventArgs e)
        {
            OnPropertyChanged("ChatCodes");
        }

        private void OnGlobalHotKeyPressed(object sender, GlobalHotKeyEventArgs e)
        {
            if (ShowHideChatKeys != null)
            {
                if (e.HotKey.Name == ShowHideChatKeys.Name)
                {
                    if (this.IsWindowVisible)
                        IsHiddenByUser = true;
                    else
                        IsHiddenByUser = false;

                    IsWindowVisible = !IsWindowVisible;
                }
            }

            if (ClickThoughtChatKeys != null)
            {
                if (e.HotKey.Name == ClickThoughtChatKeys.Name)
                {
                    IsClickThrough = !IsClickThrough;
                }
            }

            if (ClearChatKeys != null)
            {
                if (e.HotKey.Name == ClearChatKeys.Name)
                {
                    _ = _requestChatClear.InvokeAsync(new TatruEventArgs(this));
                }
            }
        }

        private BindingList<ChatCodeViewModel> LoadChatCodes(List<ChatCodeViewModel> UserChatCodes,
            List<ChatMsgType> allChatCodes)
        {
            List<ChatMsgType> chatCodes = allChatCodes.Select(entry => new ChatMsgType(entry)).ToList();
            List<ChatCodeViewModel> chatCodesViewMode = new List<ChatCodeViewModel>();
            var isNewWindow = UserChatCodes == null || UserChatCodes.Count == 0;

            foreach (var code in allChatCodes)
            {
                bool isChecked = (code.MsgType == MsgType.Translate);
                if (isNewWindow && IsDelayedDialogCode(code.ChatCode))
                {
                    isChecked = false;
                }

                chatCodesViewMode.Add(new ChatCodeViewModel(code.ChatCode, code.Name, code.Color, isChecked));
            }

            foreach (var userCode in UserChatCodes ?? Enumerable.Empty<ChatCodeViewModel>())
            {
                var code = chatCodesViewMode.FirstOrDefault(x => x.Equals(userCode));
                if (code != null)
                {
                    code.IsChecked = userCode.IsChecked;

                    if (userCode.Color.A != 0)
                    {
                        code.Color = userCode.Color;
                    }
                }
            }

            return new BindingList<ChatCodeViewModel>(chatCodesViewMode);
        }

        private static bool IsDelayedDialogCode(string chatCode)
        {
            return string.Equals(chatCode, "003D", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(chatCode, "0044", StringComparison.OrdinalIgnoreCase);
        }

        private void TrySetLangugue(CollectionView collection, TranslatorLanguague languague)
        {
            if (languague == null)
                collection.MoveCurrentToFirst();
            else
            {
                if (collection.Contains(languague))
                    collection.MoveCurrentTo(languague);
                else
                    collection.MoveCurrentToFirst();
            }
        }

        private void ShowChatWindow()
        {
            if (this.IsWindowVisible)
                IsHiddenByUser = true;
            else
                IsHiddenByUser = false;

            this.IsWindowVisible = !this.IsWindowVisible;
        }

        private void ResetChatWindowPosition()
        {
            var rect = ChatWindowRectangle;
            rect.X = 0;
            rect.Y = 0;
            rect.Width = 480;
            rect.Height = 320;

            ChatWindowRectangle = rect;
        }

        public override string ToString()
        {
            return Name;
        }

        private void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            var ea = new AsyncPropertyChangedEventArgs(this, prop);
            _asyncPropertyChanged.InvokeAsync(ea).Forget();

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }

        private void EventErrorHandler(string evname, Exception ex)
        {
            string text = evname + Environment.NewLine + Convert.ToString(ex);
            _Logger.WriteLog(text);
        }

        public void Dispose()
        {
            Dispose(true);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (_ShowHideChat != null)
                            _HotKeyManager.RemoveGlobalHotKey(_ShowHideChat);

                        if (_ClickThoughtChat != null)
                            _HotKeyManager.RemoveGlobalHotKey(_ClickThoughtChat);

                        if (_ClearChat != null)
                            _HotKeyManager.RemoveGlobalHotKey(_ClearChat);
                    }
                    catch (Exception e)
                    {
                        _Logger.WriteLog(e);
                    }
                }

                // Indicate that the instance has been disposed.
                _disposed = true;
            }
        }
    }
}