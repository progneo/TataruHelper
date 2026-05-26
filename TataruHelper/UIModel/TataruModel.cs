// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Threading.Tasks;
using System.Windows;

using FFXIVTataruHelper.Compatibility.HotKeys;
using FFXIVTataruHelper.Factories;
using FFXIVTataruHelper.FFHandlers;
using FFXIVTataruHelper.Services.HotKeys;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.Services.UI;
using FFXIVTataruHelper.ViewModel;

using Translation.Core;

namespace FFXIVTataruHelper
{
    public class TataruModel
    {
        #region **Properties.

        public TataruUIModel TataruUIModel
        {
            get { return _TataruUIModel; }
        }

        public IFFMemoryReaderService FFMemoryReader
        {
            get { return _FFMemoryReader; }
        }

        public WindowState FFWindowState
        {
            get { return FFMemoryReader.FFWindowState; }
        }

        public ChatProcessor ChatProcessor
        {
            get { return _ChatProcessor; }
        }


        public WebTranslator WebTranslator
        {
            get { return _WebTranslator; }
        }


        public TataruViewModel TataruViewModel
        {
            get { return _TataruViewModel; }
        }

        public HotKeyManager HotKeyManager
        {
            get { return _HotKeyManager; }
        }

        #endregion

        #region **Events.

        #endregion

        #region **LocalVariables.

        MainWindow _MainWindow;

        TataruViewModel _TataruViewModel;

        TataruUIModel _TataruUIModel;

        readonly IFFMemoryReaderService _FFMemoryReader;

        WebTranslator _WebTranslator;

        HotKeyManager _HotKeyManager;

        ChatProcessor _ChatProcessor;

        readonly IAppLogger _Logger;
        readonly ISettingsStore _SettingsStore;
        readonly IUiDispatcher _UiDispatcher;
        readonly IHotKeyBindingService _HotKeyBindingService;
        readonly IChatWindowCoordinator _ChatWindowCoordinator;
        readonly IApplicationCoordinator _ApplicationCoordinator;

        #endregion

        public TataruModel(
            MainWindow mainWindow,
            IAppLogger logger,
            ISettingsStore settingsStore,
            IUiDispatcher uiDispatcher,
            IFFMemoryReaderService ffMemoryReader,
            WebTranslator webTranslator,
            IHotKeyBindingService hotKeyBindingService,
            IChatWindowCoordinator chatWindowCoordinator,
            IApplicationCoordinator applicationCoordinator,
            TranslationCredentialsViewModel translationCredentials,
            IOutgoingChatWindowFactory outgoingChatWindowFactory = null)
        {
            CmdArgsStatus.LoadArgs();

            _Logger = logger;
            _SettingsStore = settingsStore;
            _UiDispatcher = uiDispatcher;
            _HotKeyBindingService = hotKeyBindingService;
            _ChatWindowCoordinator = chatWindowCoordinator;
            _ApplicationCoordinator = applicationCoordinator;

            _MainWindow = mainWindow;

            _HotKeyManager = new HotKeyManager(mainWindow);

            _WebTranslator = webTranslator;

            _TataruUIModel = new TataruUIModel(_SettingsStore, _UiDispatcher, _Logger);

            outgoingChatWindowFactory?.Bind(_TataruUIModel, mainWindow);

            _FFMemoryReader = ffMemoryReader;

            _ChatProcessor = new ChatProcessor(_WebTranslator, _SettingsStore, _Logger);

            _TataruViewModel = new TataruViewModel(this, _Logger, _UiDispatcher, _HotKeyBindingService,
                translationCredentials);
        }

        public async Task InitializeComponent()
        {
            await _ApplicationCoordinator.InitializeAsync(this, _MainWindow, _TataruUIModel, _TataruViewModel);
        }

        public void Stop()
        {
            _ApplicationCoordinator.Stop(_ChatWindowCoordinator);
        }

        public Task StopAsync()
        {
            return _ApplicationCoordinator.StopAsync(_ChatWindowCoordinator);
        }

        public async Task AsyncLoadSettings()
        {
            await Task.Run(() =>
            {
                LoadSettings();
            });
        }

        public void LoadSettings()
        {
            try
            {
                _ApplicationCoordinator.LoadSettings(
                    _TataruUIModel,
                    _SettingsStore.SystemSettingsPath,
                    _ChatProcessor,
                    _WebTranslator,
                    SaveSettings);
            }
            catch (Exception e)
            {
                _Logger.WriteLog("TataruModel.LoadSettings failed.");
                _Logger.WriteLog(e);
                throw;
            }
        }

        public async Task SaveSettings()
        {
            await Task.Run(() =>
            {
                try
                {
                    var userSettings = TataruUIModel.GetSettings();
                    Helper.SaveJson(userSettings, _SettingsStore.SettingsPath);
                }
                catch (Exception e)
                {
                    _Logger.WriteLog("TataruModel.SaveSettings failed.");
                    _Logger.WriteLog(e);
                    throw;
                }
            });
        }
    }
}