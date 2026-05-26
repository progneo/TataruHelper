using System;
using System.Windows.Input;

using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.OutgoingChat;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.ViewModel;

using Translation.Core;
using Translation.OutgoingChat;

namespace FFXIVTataruHelper.Factories
{
    public sealed class OutgoingChatWindowFactory : IOutgoingChatWindowFactory
    {
        private readonly IOutgoingChatService _outgoingChatService;
        private readonly WebTranslator _webTranslator;
        private readonly IMessageSanitizer _sanitizer;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IHotKeyBindingService _hotKeyBindingService;
        private readonly IAppLogger _logger;

        private TataruUIModel _tataruUIModel;
        private MainWindow _mainWindow;
        private HotKeyManager _hotKeyManager;
        private GlobalHotKey _showHideHotKey;
        private OutgoingChatWindow _window;

        public OutgoingChatWindowFactory(
            IOutgoingChatService outgoingChatService,
            WebTranslator webTranslator,
            IMessageSanitizer sanitizer,
            IUiDispatcher uiDispatcher,
            IHotKeyBindingService hotKeyBindingService,
            IAppLogger logger)
        {
            _outgoingChatService = outgoingChatService;
            _webTranslator = webTranslator;
            _sanitizer = sanitizer;
            _uiDispatcher = uiDispatcher;
            _hotKeyBindingService = hotKeyBindingService;
            _logger = logger;
        }

        public void Bind(TataruUIModel tataruUIModel, MainWindow mainWindow, HotKeyManager hotKeyManager)
        {
            _tataruUIModel = tataruUIModel;
            _mainWindow = mainWindow;

            if (_hotKeyManager != null && _hotKeyManager != hotKeyManager)
            {
                _hotKeyManager.GlobalHotKeyPressed -= OnGlobalHotKeyPressed;
            }

            _hotKeyManager = hotKeyManager;

            if (_hotKeyManager != null)
            {
                _hotKeyManager.GlobalHotKeyPressed += OnGlobalHotKeyPressed;
                ReapplyHotKey();
            }
        }

        public void ReapplyHotKey()
        {
            if (_hotKeyManager == null || _tataruUIModel == null)
            {
                return;
            }

            try
            {
                var combination = _tataruUIModel.OutgoingChat?.ShowHideKey;
                if (combination == null)
                {
                    if (_showHideHotKey != null)
                    {
                        _hotKeyManager.RemoveGlobalHotKey(_showHideHotKey);
                        _showHideHotKey = null;
                    }

                    return;
                }

                _hotKeyBindingService.ReRegisterGlobalHotKey(_hotKeyManager, ref _showHideHotKey, combination, false);
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex);
            }
        }

        public OutgoingChatWindow GetOrCreate()
        {
            if (_window != null)
            {
                return _window;
            }

            if (_tataruUIModel == null)
            {
                throw new InvalidOperationException(
                    "OutgoingChatWindowFactory has not been bound to a TataruUIModel yet.");
            }

            OutgoingChatWindow window = null;
            _uiDispatcher.Invoke(() =>
            {
                var viewModel = new OutgoingChatViewModel(
                    _outgoingChatService,
                    _webTranslator,
                    _tataruUIModel,
                    _sanitizer);

                window = new OutgoingChatWindow(viewModel, _logger, _mainWindow);
            });

            _window = window;
            return _window;
        }

        public void CaptureHotKeyDown(KeyEventArgs e)
        {
            if (_hotKeyManager == null || _tataruUIModel?.OutgoingChat?.ShowHideKey == null)
            {
                return;
            }

            _hotKeyBindingService.RegisterHotKeyDown(
                _hotKeyManager, ref _showHideHotKey, _tataruUIModel.OutgoingChat.ShowHideKey, e, false);
        }

        public void CaptureHotKeyUp(KeyEventArgs e)
        {
            if (_hotKeyManager == null || _tataruUIModel?.OutgoingChat?.ShowHideKey == null)
            {
                return;
            }

            _hotKeyBindingService.RegisterHotKeyUp(
                _hotKeyManager, ref _showHideHotKey, _tataruUIModel.OutgoingChat.ShowHideKey, e, false);
        }

        private void OnGlobalHotKeyPressed(object sender, GlobalHotKeyEventArgs e)
        {
            if (_showHideHotKey == null || e?.HotKey == null)
            {
                return;
            }

            if (!string.Equals(e.HotKey.Name, _showHideHotKey.Name, StringComparison.Ordinal))
            {
                return;
            }

            _uiDispatcher.InvokeAsync(() =>
            {
                try
                {
                    var window = GetOrCreate();
                    if (window.IsVisible)
                    {
                        window.Hide();
                    }
                    else
                    {
                        window.ShowAndActivate();
                    }
                }
                catch (Exception ex)
                {
                    _logger?.WriteLog(ex);
                }
            });
        }
    }
}