using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.TataruComponentModel;
using FFXIVTataruHelper.ViewModel;
using System;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FFXIVTataruHelper.Services.UI
{
    public sealed class ChatWindowsEventCoordinator : IChatWindowsEventCoordinator
    {
        private readonly IUiDispatcher _uiDispatcher;
        private readonly IAppLogger _logger;
        private readonly IChatWindowCoordinator _chatWindowCoordinator;
        private readonly SemaphoreSlim _deletionGate = new SemaphoreSlim(1, 1);

        private TataruUIModel _uiModel;
        private TataruViewModel _viewModel;
        private TataruModel _tataruModel;
        private MainWindow _mainWindow;
        private bool _isStarted;

        public ChatWindowsEventCoordinator(IUiDispatcher uiDispatcher, IAppLogger logger, IChatWindowCoordinator chatWindowCoordinator)
        {
            _uiDispatcher = uiDispatcher;
            _logger = logger;
            _chatWindowCoordinator = chatWindowCoordinator;
        }

        public void Start(TataruUIModel uiModel, TataruViewModel viewModel, TataruModel tataruModel, MainWindow mainWindow)
        {
            if (_isStarted)
            {
                return;
            }

            _uiModel = uiModel ?? throw new ArgumentNullException(nameof(uiModel));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            _tataruModel = tataruModel ?? throw new ArgumentNullException(nameof(tataruModel));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));

            _uiModel.ChatWindowsListChangedAsync += OnSettingsWindowsListChangedAsync;
            _viewModel.ChatWindowsListChangedAsync += OnViewModelWindowsListChangedAsync;
            _viewModel.ChatWindowsListChangedAsync += OnViewModelChatWindowsListChangedAsync;

            _isStarted = true;
        }

        public void Stop()
        {
            if (!_isStarted)
            {
                return;
            }

            _uiModel.ChatWindowsListChangedAsync -= OnSettingsWindowsListChangedAsync;
            _viewModel.ChatWindowsListChangedAsync -= OnViewModelWindowsListChangedAsync;
            _viewModel.ChatWindowsListChangedAsync -= OnViewModelChatWindowsListChangedAsync;

            _uiModel = null;
            _viewModel = null;
            _tataruModel = null;
            _mainWindow = null;
            _isStarted = false;
        }

        private async Task OnSettingsWindowsListChangedAsync(AsyncListChangedEventHandler<ChatWindowViewModelSettings> ea)
        {
            switch (ea.ChangedEventArgs.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                {
                    await _uiDispatcher.InvokeAsync(() =>
                    {
                        ChatWindowViewModelSettings newElem = ea.ChangedElemnt;

                        var existing = _viewModel.ChatWindows.FirstOrDefault(x => x.WinId == newElem.WinId);
                        if (existing == null)
                        {
                            try
                            {
                                _chatWindowCoordinator.AddFromSettings(newElem, _viewModel);
                            }
                            catch (OperationCanceledException)
                            {
                                _logger.WriteLog("ChatWindowsEventCoordinator.OnSettingsWindowsListChangedAsync add canceled.");
                            }
                            catch (Exception e)
                            {
                                _logger.WriteLog("ChatWindowsEventCoordinator.OnSettingsWindowsListChangedAsync add failed.");
                                _logger.WriteLog(e);
                            }
                        }
                    });
                    break;
                }
                case ListChangedType.ItemDeleted:
                {
                    await _deletionGate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await _uiDispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                ChatWindowViewModelSettings deletedElem = ea.ChangedElemnt;
                                var existing = _viewModel.ChatWindows.FirstOrDefault(x => x.WinId == deletedElem.WinId);

                                if (existing != null)
                                {
                                    _chatWindowCoordinator.RemoveFromSettings(deletedElem, _viewModel);
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.WriteLog("ChatWindowsEventCoordinator.OnSettingsWindowsListChangedAsync delete failed.");
                                _logger.WriteLog(e);
                            }
                        });
                    }
                    finally
                    {
                        _deletionGate.Release();
                    }

                    break;
                }
            }
        }

        private async Task OnViewModelWindowsListChangedAsync(AsyncListChangedEventHandler<ChatWindowViewModel> ea)
        {
            switch (ea.ChangedEventArgs.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                {
                    await _uiDispatcher.InvokeAsync(() =>
                    {
                        ChatWindowViewModel newElem = ea.ChangedElemnt;
                        var existing = _uiModel.ChatWindows.FirstOrDefault(x => x.WinId == newElem.WinId);

                        if (existing == null)
                        {
                            _chatWindowCoordinator.AddFromViewModel(newElem, _uiModel);
                        }
                    });
                    break;
                }
                case ListChangedType.ItemDeleted:
                {
                    await _deletionGate.WaitAsync().ConfigureAwait(false);
                    try
                    {
                        await _uiDispatcher.InvokeAsync(() =>
                        {
                            try
                            {
                                ChatWindowViewModel deletedElem = ea.ChangedElemnt;
                                var existing = _uiModel.ChatWindows.FirstOrDefault(x => x.WinId == deletedElem.WinId);

                                if (existing != null)
                                {
                                    _chatWindowCoordinator.RemoveFromViewModel(deletedElem, _uiModel);
                                }
                            }
                            catch (Exception e)
                            {
                                _logger.WriteLog("ChatWindowsEventCoordinator.OnViewModelWindowsListChangedAsync delete failed.");
                                _logger.WriteLog(e);
                            }
                        });
                    }
                    finally
                    {
                        _deletionGate.Release();
                    }

                    break;
                }
            }
        }

        private async Task OnViewModelChatWindowsListChangedAsync(AsyncListChangedEventHandler<ChatWindowViewModel> ea)
        {
            switch (ea.ChangedEventArgs.ListChangedType)
            {
                case ListChangedType.ItemAdded:
                {
                    try
                    {
                        await _uiDispatcher.InvokeAsync(() =>
                        {
                            ChatWindowViewModel newElem = ea.ChangedElemnt;
                            _chatWindowCoordinator.ShowChatWindow(_tataruModel, newElem, _mainWindow);
                        });
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.WriteLog("ChatWindowsEventCoordinator.OnViewModelChatWindowsListChangedAsync add canceled.");
                    }
                    catch (Exception e)
                    {
                        _logger.WriteLog("ChatWindowsEventCoordinator.OnViewModelChatWindowsListChangedAsync add failed.");
                        _logger.WriteLog(e);
                    }

                    break;
                }
                case ListChangedType.ItemDeleted:
                {
                    break;
                }
            }
        }
    }
}
