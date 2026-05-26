using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.ViewModel;

using Translation.Core;

namespace FFXIVTataruHelper.Factories
{
    public sealed class TataruModelFactory : ITataruModelFactory
    {
        private readonly IAppLogger _logger;
        private readonly ISettingsStore _settingsStore;
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IFFMemoryReaderService _ffMemoryReader;
        private readonly WebTranslator _webTranslator;
        private readonly IHotKeyBindingService _hotKeyBindingService;
        private readonly IChatWindowCoordinator _chatWindowCoordinator;
        private readonly IApplicationCoordinator _applicationCoordinator;
        private readonly TranslationCredentialsViewModel _translationCredentials;
        private readonly IOutgoingChatWindowFactory _outgoingChatWindowFactory;

        public TataruModelFactory(
            IAppLogger logger,
            ISettingsStore settingsStore,
            IUiDispatcher uiDispatcher,
            IFFMemoryReaderService ffMemoryReader,
            WebTranslator webTranslator,
            IHotKeyBindingService hotKeyBindingService,
            IChatWindowCoordinator chatWindowCoordinator,
            IApplicationCoordinator applicationCoordinator,
            TranslationCredentialsViewModel translationCredentials,
            IOutgoingChatWindowFactory outgoingChatWindowFactory)
        {
            _logger = logger;
            _settingsStore = settingsStore;
            _uiDispatcher = uiDispatcher;
            _ffMemoryReader = ffMemoryReader;
            _webTranslator = webTranslator;
            _hotKeyBindingService = hotKeyBindingService;
            _chatWindowCoordinator = chatWindowCoordinator;
            _applicationCoordinator = applicationCoordinator;
            _translationCredentials = translationCredentials;
            _outgoingChatWindowFactory = outgoingChatWindowFactory;
        }

        public TataruModel Create(MainWindow mainWindow)
        {
            return new TataruModel(mainWindow, _logger, _settingsStore, _uiDispatcher, _ffMemoryReader, _webTranslator,
                _hotKeyBindingService, _chatWindowCoordinator, _applicationCoordinator, _translationCredentials,
                _outgoingChatWindowFactory);
        }
    }
}