// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.ViewModel;
using FFXIVTataruHelper.WinUtils;

using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

using FFXIVTataruHelper.TataruComponentModel;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// Interaction logic for ChatWindow.xaml
    /// </summary>
    public partial class ChatWindow : Window
    {
        public IntPtr WindowHandle
        {
            get { return new WindowInteropHelper(this).Handle; }
        }

        public long WinId
        {
            get { return _ChatWindowViewModel.WinId; }
        }

        private WindowResizer _WindowResizer;

        private bool _IsClickThrought = false;

        private DateTime _TextArrivedTime;
        protected bool _KeepWorking;
        private bool _AutoHidden;

        TataruModel _TataruModel;
        ChatWindowViewModel _ChatWindowViewModel;

        MainWindow _MainWindow;
        readonly IAppLogger _Logger;
        readonly ISettingsStore _SettingsStore;
        readonly IUiDispatcher _UiDispatcher;

        public ChatWindow(
            TataruModel tataruModel,
            ChatWindowViewModel chatWindowViewModel,
            MainWindow mainWindow,
            IAppLogger logger,
            ISettingsStore settingsStore,
            IUiDispatcher uiDispatcher)
        {
            InitializeComponent();

            try
            {
                _MainWindow = mainWindow;

                _TataruModel = tataruModel;
                _ChatWindowViewModel = chatWindowViewModel;
                _Logger = logger;
                _SettingsStore = settingsStore;
                _UiDispatcher = uiDispatcher;

                this.DataContext = _ChatWindowViewModel;

                this.ShowInTaskbar = false;

                ChatRtb.AcceptsTab = true;

                ChatRtb.BorderThickness = new Thickness(0);

                ChatRtb.Document.Blocks.Clear();

                ChatRtb.IsUndoEnabled = false;

                _TextArrivedTime = DateTime.UtcNow;

                _KeepWorking = true;
                _AutoHidden = false;
                _WindowResizer = new WindowResizer(this, _Logger);
            }
            catch (Exception e)
            {
                _Logger.WriteLog(e);
            }
        }

        #region **UserActions.

        public async Task ClearChat()
        {
            await this.UIThreadAsync(() =>
            {
                ChatRtb.Document.Blocks.Clear();
            });
        }

        #endregion

        #region **WindowEvents.

        protected virtual void Window_Loaded(object sender, RoutedEventArgs e)
        {
            AutoHideStatusCheck();

            _TataruModel.FFMemoryReader.AddExclusionWindowHandler((new WindowInteropHelper(this).Handle));

            if (_ChatWindowViewModel.IsClickThrough)
                MakeWindowClickThrough();
            else
                MakeWindowClickable();

            _ChatWindowViewModel.AsyncPropertyChanged += OnSettingsWindowPropertyChange;
            _ChatWindowViewModel.RequestChatClear += OnChatClearRequest;

            _TataruModel.ChatProcessor.TextArrived += OnTextArrived;
            _TataruModel.FFMemoryReader.FFWindowStateChanged += OnFFWindowStateChange;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            _KeepWorking = false;
        }

        protected virtual void Window_Deactivated(object sender, EventArgs e)
        {
            int t = 0;
            t++;
        }

        #endregion

        #region **UiEvents.

        protected async Task OnTextArrived(ChatMessageArrivedEventArgs ea)
        {
            string text = "";
            Color textColor = Color.FromArgb(255, 255, 255, 255);
            ChatCodeViewModel chatCode;

            chatCode = _ChatWindowViewModel.ChatCodes.FirstOrDefault(x => x.Code == ea.ChatMessage.Code);

            if (chatCode == null)
                return;
            if (!chatCode.IsChecked)
                return;

            textColor = chatCode.Color;

            int translateTryCount = 0;
            bool notTransalted = true;

            if (ea.ChatMessage.Text.Length > 0)
            {
                while (translateTryCount < _SettingsStore.MaxTranslateTryCount && notTransalted)
                {
                    var translationEngines = _TataruModel.ChatProcessor.TranslationEngines;
                    string translation = string.Empty;


                    using (var translationCts = new CancellationTokenSource(_SettingsStore.TranslatorWaitTimeMs))
                    {
                        try
                        {
                            translation = await _TataruModel.ChatProcessor.Translate(
                                ea.ChatMessage.Text,
                                _ChatWindowViewModel.CurrentTransaltionEngine,
                                _ChatWindowViewModel.CurrentTranslateFromLanguague,
                                _ChatWindowViewModel.CurrentTranslateToLanguague,
                                chatCode.Code,
                                translationCts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            translation = string.Empty;
                        }
                    }

                    translateTryCount++;

                    if (translation.Length < 1)
                    {
                        var engineIndex = translationEngines.IndexOf(_ChatWindowViewModel.CurrentTransaltionEngine);
                        if (engineIndex < 0)
                            engineIndex = translationEngines.Count - 1;

                        bool supported = false;

                        int iterCount = 0;
                        do
                        {
                            engineIndex++;
                            iterCount++;
                            if (engineIndex >= translationEngines.Count)
                                engineIndex = 0;

                            var tmpEngine = translationEngines[engineIndex];
                            if (tmpEngine.SupportedLanguages.Contains(
                                    _ChatWindowViewModel.CurrentTranslateFromLanguague)
                                && tmpEngine.SupportedLanguages.Contains(_ChatWindowViewModel
                                    .CurrentTranslateToLanguague))
                            {
                                supported = true;

                                _UiDispatcher.Invoke(() =>
                                {
                                    _ChatWindowViewModel.TranslationEngines.MoveCurrentToPosition(engineIndex);
                                });
                            }
                        } while (!supported && iterCount <= translationEngines.Count);

                        _UiDispatcher.Invoke(() =>
                        {
                            ShowErrorText(1, _ChatWindowViewModel.CurrentTransaltionEngine.Name, textColor);
                        });
                    }
                    else
                    {
                        text = translation;
                        notTransalted = false;
                    }
                }
            }
            else
            {
                text = string.Empty;
            }

            DateTime timeStamp = default(DateTime);

            if (_ChatWindowViewModel.ShowTimestamps)
                timeStamp = ea.ChatMessage.TimeStamp;

            await this.UIThreadAsync(() =>
            {
                ShowWindow();

                if (_ChatWindowViewModel.IsHiddenByUser == false)
                    _TextArrivedTime = DateTime.UtcNow;

                ShowTranslatedText(text, textColor, timeStamp);

                if (_ChatWindowViewModel.IsHiddenByUser == false)
                    _TextArrivedTime = DateTime.UtcNow;
            });
        }

        protected virtual async Task OnSettingsWindowPropertyChange(AsyncPropertyChangedEventArgs ea)
        {
            switch (ea.PropertyName)
            {
                case "IsClickThrough":

                    if (_ChatWindowViewModel.IsClickThrough)
                        MakeWindowClickThrough();
                    else
                        MakeWindowClickable();

                    break;
                case "IsAutoHide":
                    {
                        _TextArrivedTime = DateTime.UtcNow;
                        if (!_ChatWindowViewModel.IsAutoHide)
                            ShowWindow();
                    }
                    break;
                case "IsWindowVisible":
                    {
                        if (_ChatWindowViewModel.IsWindowVisible == true)
                            _TextArrivedTime = DateTime.UtcNow;
                    }
                    break;
                case "BackGroundColor":
                    {
                        if (_ChatWindowViewModel.BackGroundColor.A == 255)
                            this.AllowsTransparency = false;
                        else
                            this.AllowsTransparency = true;

                        if (_ChatWindowViewModel.IsClickThrough)
                        {
                            MakeWindowClickable();
                            MakeWindowClickThrough();
                        }
                    }
                    break;
            }
        }

        protected virtual async Task OnChatClearRequest(TatruEventArgs ea)
        {
            await ClearChat();
        }

        protected virtual async Task OnFFWindowStateChange(WindowStateChangeEventArgs ea)
        {
            await this.UIThreadAsync(() =>
            {
                if (ea.IsRunningNew != ea.IsRunningOld)
                {
                    //_TataruUIModel.IsHiddenByUser = false;

                    _ChatWindowViewModel.IsHiddenByUser = false;

                    //_TextArrivedTime = DateTime.UtcNow;

                    //_AutoHidden = false;
                }

                if (ea.NewWindowState != ea.OldWindowState)
                {
                    if (ea.NewWindowState == WindowState.Minimized)
                    {
                        _ChatWindowViewModel.IsWindowVisible = false;
                    }
                    else if (_ChatWindowViewModel.IsHiddenByUser == false)
                    {
                        if (_AutoHidden == false)
                            _ChatWindowViewModel.IsWindowVisible = true;
                    }
                }
            });
        }

        #endregion

        #region **Transaltion.

        void ShowTranslatedText(string translatedMsg, Color color, DateTime timeStamp = default(DateTime))
        {
            try
            {
                translatedMsg = translatedMsg.Trim(new char[] { ' ' });

                ChatRtb.AppendText(Environment.NewLine);
                ChatRtb.CaretPosition = ChatRtb.CaretPosition.DocumentEnd;

                if (_ChatWindowViewModel.SpacingCount > 0)
                {
                    string whiteSpaces = String.Empty;
                    for (int i = 0; i < _ChatWindowViewModel.SpacingCount; i++)
                    {
                        whiteSpaces += " ";
                    }

                    ChatRtb.AppendText(whiteSpaces);
                }

                Paragraph p = (ChatRtb.Document.Blocks.LastBlock) as Paragraph;
                p.Margin = new Thickness(0, _ChatWindowViewModel.LineBreakHeight, 0, 0);

                SolidColorBrush tmpColor = new SolidColorBrush(color);

                int nameInd = 0;
                if ((nameInd = translatedMsg.IndexOf(":")) > 0)
                {
                    string msgText = translatedMsg;
                    string name = String.Empty;
                    string text = String.Empty;

                    name = msgText.Substring(0, nameInd);
                    text = msgText.Substring(nameInd, msgText.Length - nameInd);

                    if (timeStamp != default(DateTime))
                        name = timeStamp.ToString("HH:mm") + " " + name;

                    TextRange tr1 = new TextRange(ChatRtb.Document.ContentEnd, ChatRtb.Document.ContentEnd);
                    tr1.Text = name;
                    tr1.ApplyPropertyValue(TextElement.ForegroundProperty, tmpColor);
                    tr1.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Bold);

                    TextRange tr2 = new TextRange(ChatRtb.Document.ContentEnd, ChatRtb.Document.ContentEnd);

                    tr2.Text = text;
                    tr2.ApplyPropertyValue(TextElement.ForegroundProperty, tmpColor);
                    tr2.ApplyPropertyValue(TextElement.FontWeightProperty, FontWeights.Normal); //*/
                }
                else
                {
                    if (timeStamp != default(DateTime))
                        translatedMsg = timeStamp.ToString("HH:mm") + " " + translatedMsg;

                    TextRange tr = new TextRange(ChatRtb.Document.ContentEnd, ChatRtb.Document.ContentEnd);
                    tr.Text = translatedMsg;

                    tr.ApplyPropertyValue(TextElement.ForegroundProperty, tmpColor);
                }

                ChatRtb.ScrollToEnd();
            }
            catch (Exception exc)
            {
                _Logger.WriteLog(Convert.ToString(exc));
            }
        }

        void ShowErrorText(int errorCode, string EngineName, Color textColor)
        {
            if (errorCode == 1)
            {
                //string text = ((string)_SettigsWindow.Resources["TranslationEngineSwitchMsg"]) + " " + Convert.ToString(_TataruUIModel.TranslationEngine);
                string text = ((string)Application.Current.Resources["TranslationEngineSwitchMsg"]) + EngineName;

                ShowWindow();

                if (_ChatWindowViewModel.IsHiddenByUser == false)
                    _TextArrivedTime = DateTime.UtcNow;

                ShowTranslatedText(text, textColor);

                if (_ChatWindowViewModel.IsHiddenByUser == false)
                    _TextArrivedTime = DateTime.UtcNow;
            }
        }

        #endregion

        #region **WindowResize.

        // for each rectangle, assign the following method to its MouseEnter event.
        protected virtual void DisplayResizeCursor(object sender, MouseEventArgs e)
        {
            _WindowResizer.displayResizeCursor(sender);
        }

        protected virtual void DisplayDragCursor(object sender, MouseEventArgs e)
        {
            _WindowResizer.DisplayDragCursor(sender);
        }

        // for each rectangle, assign the following method to its MouseLeave event.
        protected virtual void ResetCursor(object sender, MouseEventArgs e)
        {
            _WindowResizer.resetCursor();
        }

        // for each rectangle, assign the following method to its PreviewMouseDown event.
        protected virtual void Resize(object sender, MouseButtonEventArgs e)
        {
            _WindowResizer.resizeWindow(sender);
        }

        // finally, you may use the following method to enable dragging!
        protected virtual void Drag(object sender, MouseButtonEventArgs e)
        {
            _WindowResizer.dragWindow(sender, e);
        }

        #endregion

        #region **System.

        protected void MakeWindowClickThrough()
        {
            try
            {
                if (!_IsClickThrought)
                {
                    this.UIThread(() =>
                    {
                        var hwnd = new WindowInteropHelper(this).Handle;
                        var style = Win32Interfaces.GetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE);
                        Win32Interfaces.SetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE,
                            style | Win32Interfaces.WS_EX_LAYERED | Win32Interfaces.WS_EX_TRANSPARENT);
                        _IsClickThrought = true;
                    });
                }
            }
            catch (Exception e)
            {
                _Logger.WriteLog(Convert.ToString(e));
            }
        }

        protected void MakeWindowClickable()
        {
            try
            {
                if (_IsClickThrought)
                {
                    this.UIThread(() =>
                    {
                        var hwnd = new WindowInteropHelper(this).Handle;
                        var style = Win32Interfaces.GetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE);
                        Win32Interfaces.SetWindowLong(hwnd, Win32Interfaces.GWL_EXSTYLE,
                            style ^ Win32Interfaces.WS_EX_LAYERED ^ Win32Interfaces.WS_EX_TRANSPARENT);
                        _IsClickThrought = false;
                    });
                }
            }
            catch (Exception e)
            {
                _Logger.WriteLog(Convert.ToString(e));
            }
        }

        void HideThisWindow_Click(object sender, RoutedEventArgs e)
        {
            _ChatWindowViewModel.IsHiddenByUser = true;

            _ChatWindowViewModel.IsWindowVisible = false;
        }

        void Settings_Click(object sender, RoutedEventArgs e)
        {
            /*
            Helper.Unminimize(_SettigsWindow);

            _SettigsWindow.Visibility = Visibility.Visible;
            _SettigsWindow.Activate();
            _SettigsWindow.Focus();//*/
            _MainWindow.ShowSettingsWindow();
        }

        void Exit_Click(object sender, RoutedEventArgs e)
        {
            Application.Current.Shutdown();
        }

        private void ShowWindow()
        {
            if (this.Visibility != Visibility.Visible)
            {
                if (_ChatWindowViewModel.IsHiddenByUser == false)
                {
                    if (_TataruModel.FFMemoryReader.FFWindowState != WindowState.Minimized)
                        this.Show();
                }
            }
        }

        protected virtual void AutoHideStatusCheck()
        {
            Task.Factory.StartNew(async () =>
            {
                while (_KeepWorking)
                {
                    if (_ChatWindowViewModel.IsAutoHide)
                    {
                        var ts = DateTime.UtcNow - _TextArrivedTime;
                        if (ts > _ChatWindowViewModel.AutoHideTimeout)
                        {
                            this.UIThread(() =>
                            {
                                _AutoHidden = true;
                                _ChatWindowViewModel.IsWindowVisible = false;
                            });
                        }
                        else
                            _AutoHidden = false;
                    }
                    else
                        _AutoHidden = false;

                    await Task.Delay(_SettingsStore.AutoHideWatcherDelayMs);
                }
            }, TaskCreationOptions.LongRunning);
        }

        protected override void OnClosed(EventArgs e)
        {
            _ChatWindowViewModel.AsyncPropertyChanged -= OnSettingsWindowPropertyChange;
            _ChatWindowViewModel.RequestChatClear -= OnChatClearRequest;

            _TataruModel.ChatProcessor.TextArrived -= OnTextArrived;
            _TataruModel.FFMemoryReader.FFWindowStateChanged -= OnFFWindowStateChange;

            base.OnClosed(e);
        }

        #endregion
    }
}
