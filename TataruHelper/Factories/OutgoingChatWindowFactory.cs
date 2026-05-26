using System;

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
        private readonly IAppLogger _logger;

        private TataruUIModel _tataruUIModel;
        private MainWindow _mainWindow;
        private OutgoingChatWindow _window;

        public OutgoingChatWindowFactory(
            IOutgoingChatService outgoingChatService,
            WebTranslator webTranslator,
            IMessageSanitizer sanitizer,
            IUiDispatcher uiDispatcher,
            IAppLogger logger)
        {
            _outgoingChatService = outgoingChatService;
            _webTranslator = webTranslator;
            _sanitizer = sanitizer;
            _uiDispatcher = uiDispatcher;
            _logger = logger;
        }

        public void Bind(TataruUIModel tataruUIModel, MainWindow mainWindow)
        {
            _tataruUIModel = tataruUIModel;
            _mainWindow = mainWindow;
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
    }
}