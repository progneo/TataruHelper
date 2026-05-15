// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows;

using FFXIVTataruHelper.EventArguments;
using FFXIVTataruHelper.Services.GameMemory;
using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Services.Settings;
using FFXIVTataruHelper.TataruComponentModel;
using FFXIVTataruHelper.WinUtils;

using Sharlayan.Core;
using Sharlayan.Models;
using Sharlayan.Models.ReadResults;

namespace FFXIVTataruHelper.FFHandlers
{
    public class FFMemoryReader : IFFMemoryReaderService
    {
        #region **Events.

        public event AsyncEventHandler<AsyncPropertyChangedEventArgs> AsyncPropertyChanged
        {
            add => _AsyncPropertyChanged.Register(value);
            remove => _AsyncPropertyChanged.Unregister(value);
        }

        private AsyncEvent<AsyncPropertyChangedEventArgs> _AsyncPropertyChanged;

        public event AsyncEventHandler<WindowStateChangeEventArgs> FFWindowStateChanged
        {
            add => _FFWindowStateChanged.Register(value);
            remove => _FFWindowStateChanged.Unregister(value);
        }

        private AsyncEvent<WindowStateChangeEventArgs> _FFWindowStateChanged;

        public event AsyncEventHandler<ChatMessageArrivedEventArgs> FFChatMessageArrived
        {
            add => _FFChatMessageArrived.Register(value);
            remove => _FFChatMessageArrived.Unregister(value);
        }

        private AsyncEvent<ChatMessageArrivedEventArgs> _FFChatMessageArrived;

        #endregion

        #region **Properties.

        public System.Windows.WindowState FFWindowState
        {
            get;
            private set
            {
                field = value;
                OnPropertyChanged();
            }
        }

        public bool UseDirectReading
        {
            get => _useDirectReading;
            set => _useDirectReading = value;
        }

        #endregion

        #region **LocalVariables.

        private bool _keepWorking;
        private bool _keepReading;

        private readonly bool _useDirectReadingInternal;

        private bool _useDirectReading;

        private readonly ConcurrentDictionary<string, DateTime> _recentEmittedMessages;
        private static readonly TimeSpan DuplicateSuppressionWindow = TimeSpan.FromSeconds(2);

        private Process _ffXivProcess = null;
        private string _ffProcessName;

        private readonly List<IntPtr> _exclusionWindowHandlers;

        private readonly ConcurrentQueue<FFChatMsg> _ffxivChat;

        private readonly IGameMemoryGateway _gameMemoryGateway;
        private readonly IAppLogger _logger;
        private readonly ISettingsStore _settingsStore;

        #endregion

        public FFMemoryReader(IGameMemoryGateway gameMemoryGateway, IAppLogger logger, ISettingsStore settingsStore)
        {
            _gameMemoryGateway = gameMemoryGateway;
            _logger = logger;
            _settingsStore = settingsStore;
            _exclusionWindowHandlers = new List<IntPtr>();
            _ffxivChat = new ConcurrentQueue<FFChatMsg>();
            _recentEmittedMessages = new ConcurrentDictionary<string, DateTime>();

            _FFWindowStateChanged =
                new AsyncEvent<WindowStateChangeEventArgs>(EventErrorHandler, "FFWindowStateChanged");
            _FFChatMessageArrived =
                new AsyncEvent<ChatMessageArrivedEventArgs>(EventErrorHandler, "FFChatMessageArrived");
            _AsyncPropertyChanged =
                new AsyncEvent<AsyncPropertyChangedEventArgs>(EventErrorHandler,
                    "FFMemoryReader \n FFChatMessageArrived");

            _useDirectReadingInternal = true;
        }

        public void Start()
        {
            _keepWorking = true;
            _keepReading = true;

            FFWindowState = WindowState.Minimized;

            Task.Factory.StartNew(async () =>
            {
                try
                {
                    await EntryPoint();
                }
                catch (OperationCanceledException)
                {
                    _logger.WriteLog("FFMemoryReader.Start/EntryPoint canceled.");
                }
                catch (Exception e)
                {
                    _logger.WriteLog("FFMemoryReader.Start/EntryPoint failed.");
                    _logger.WriteLog(e);
                }
            }, TaskCreationOptions.LongRunning);
        }

        public void AddExclusionWindowHandler(IntPtr handler)
        {
            _exclusionWindowHandlers.Add(handler);
        }

        private async Task EntryPoint()
        {
            ChatMessageEvetRiser();

            while (_keepWorking)
            {
                await InitMemoryReader();

                WatchFFWindowState();

                await ChatReader();
            }
        }

        private async Task InitMemoryReader()
        {
            try
            {
                var processNotFound = true;

                const string processName = "ffxiv_dx11";
                _ffProcessName = processName;

                while (_keepWorking && processNotFound)
                {
                    var processes = Process.GetProcessesByName(_ffProcessName);
                    if (processes.Length > 0)
                    {
                        try
                        {
                            // supported: English, Chinese, Japanese, French, German, Korean
                            const string gameLanguage = "English";
                            // whether to always hit API on start to get the latest sigs based on patchVersion, or use the local json cache (if the file doesn't exist, API will be hit)
                            const bool useLocalCache = true;
                            const bool scanAllMemoryRegions = false;
                            // patchVersion of game, or latest//
                            const string patchVersion = "latest";
                            var process = processes[0];

                            if (_ffXivProcess != null)
                            {
                                _ffXivProcess.Dispose();
                            }

                            _ffXivProcess = process;
                            var processModel = new ProcessModel { Process = process };

                            _gameMemoryGateway.SetProcess(processModel, gameLanguage, patchVersion, useLocalCache,
                                scanAllMemoryRegions);

                            processNotFound = false;
                            _keepReading = true;
                        }
                        catch (OperationCanceledException)
                        {
                            _logger.WriteLog("FFMemoryReader.InitMemoryReader process attach canceled.");
                            throw;
                        }
                        catch (Exception e)
                        {
                            await Task.Delay(_settingsStore.LookForProcessDelayMs);
                            _logger.WriteLog("FFMemoryReader.InitMemoryReader process attach failed.");
                            _logger.WriteLog(e);
                        }
                    }
                    else
                    {
                        await Task.Delay(_settingsStore.LookForProcessDelayMs);
                    }
                }
            }
            catch (OperationCanceledException)
            {
                _logger.WriteLog("FFMemoryReader.InitMemoryReader canceled.");
            }
            catch (Exception e)
            {
                _logger.WriteLog("FFMemoryReader.InitMemoryReader failed.");
                _logger.WriteLog(e);
            }
        }

        private void WatchFFWindowState()
        {
            Task.Factory.StartNew(async () =>
            {
                var ffxivPrevWindowState = WindowState.Minimized;
                FFWindowState = WindowState.Minimized;

                var isRunningPrev = false;
                while (_keepWorking && _keepReading)
                {
                    try
                    {
                        var ffxivWindowState = WindowState.Normal;
                        var fgWindow = Win32Interfaces.GetForegroundWindow();

                        if (_ffXivProcess != null)
                        {
                            if (_ffXivProcess.MainWindowHandle != fgWindow)
                            {
                                ffxivWindowState = WindowState.Minimized;
                            }
                            else
                            {
                                ffxivWindowState = WindowState.Normal;
                            }
                        }

                        var isExclusionWindow = _exclusionWindowHandlers.Any(handler => fgWindow == handler);

                        if (!isExclusionWindow && fgWindow != IntPtr.Zero)
                        {
                            var oldValue = ffxivPrevWindowState;

                            if (ffxivWindowState != ffxivPrevWindowState)
                            {
                                ffxivPrevWindowState = ffxivWindowState;

                                var ea = new WindowStateChangeEventArgs(this)
                                {
                                    OldWindowState = oldValue,
                                    NewWindowState = ffxivPrevWindowState,
                                    IsRunningOld = isRunningPrev,
                                    IsRunningNew = true,
                                    Text = ""
                                };

                                _FFWindowStateChanged.InvokeAsync(ea).Forget();
                            }

                            FFWindowState = ffxivPrevWindowState;
                        }

                        var processes = Process.GetProcessesByName(_ffProcessName);
                        if (processes.Length == 0)
                        {
                            const WindowState oldState = WindowState.Normal;
                            var ea = new WindowStateChangeEventArgs(this)
                            {
                                OldWindowState = oldState,
                                NewWindowState = ffxivPrevWindowState,
                                IsRunningOld = isRunningPrev,
                                IsRunningNew = false,
                                Text = ""
                            };

                            _FFWindowStateChanged.InvokeAsync(ea).Forget();

                            _keepReading = false;

                            isRunningPrev = false;

                            FFWindowState = WindowState.Minimized;

                            _gameMemoryGateway.UnsetProcess();
                        }
                        else
                        {
                            if (isRunningPrev == false)
                            {
                                const WindowState oldState = WindowState.Minimized;
                                const WindowState newState = WindowState.Normal;
                                var ea = new WindowStateChangeEventArgs(this)
                                {
                                    OldWindowState = oldState,
                                    NewWindowState = newState,
                                    IsRunningOld = isRunningPrev,
                                    IsRunningNew = true,
                                    Text = processes[0].ProcessName + ".exe" + "  PID: " +
                                           processes[0].Id.ToString()
                                };

                                _FFWindowStateChanged.InvokeAsync(ea).Forget();

                                FFWindowState = WindowState.Normal;
                            }

                            isRunningPrev = true;
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.WriteLog("FFMemoryReader.WatchFFWindowState canceled.");
                        break;
                    }
                    catch (Exception e)
                    {
                        _logger.WriteLog("FFMemoryReader.WatchFFWindowState failed.");
                        _logger.WriteLog(e);
                    }

                    await Task.Delay(_settingsStore.MemoryReaderDelayMs);
                }
            }, TaskCreationOptions.LongRunning);
        }

        private async Task ChatReader()
        {
            var previousArrayIndex = 0;
            var previousOffset = 0;

            while (_keepWorking && _keepReading)
            {
                try
                {
                    var readResult = _gameMemoryGateway.GetChatLog(previousArrayIndex, previousOffset);
                    previousArrayIndex = readResult.PreviousArrayIndex;
                    previousOffset = readResult.PreviousOffset;

                    ProcessReadResult(readResult);
                }
                catch (OperationCanceledException)
                {
                    _logger.WriteLog("FFMemoryReader.ChatReader canceled.");
                    break;
                }
                catch (Exception e)
                {
                    _logger.WriteLog("FFMemoryReader.ChatReader read failed.");
                    _logger.WriteLog(e);
                }

                await Task.Delay(_settingsStore.MemoryReaderDelayMs);
            }
        }

        private void ProcessReadResult(ChatLogResult readResult)
        {
            var chatLogEntries = readResult?.ChatLogItems?.ToArray() ?? Array.Empty<ChatLogItem>();

            if (!_useDirectReadingInternal || !_useDirectReading)
            {
                if (chatLogEntries.Length == 0)
                {
                    return;
                }

                foreach (var chatLogItem in chatLogEntries)
                {
                    ProcessChatMsg(chatLogItem);
                }

                return;
            }

            foreach (var chatLogItem in chatLogEntries)
            {
                if (!IsDirectDialogCode(chatLogItem))
                {
                    ProcessChatMsg(chatLogItem);
                }
            }

            var directDialog = _gameMemoryGateway.GetDirectDialog();
            if (directDialog?.ChatLogItems == null || directDialog.ChatLogItems.Count == 0)
            {
                return;
            }

            foreach (var directItem in directDialog.ChatLogItems.ToArray())
            {
                ProcessChatMsg(directItem);
            }
        }

        private static bool IsDirectDialogCode(ChatLogItem chatLogItem)
        {
            if (chatLogItem == null || string.IsNullOrEmpty(chatLogItem.Code))
            {
                return false;
            }

            return string.Equals(chatLogItem.Code, "003D", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(chatLogItem.Code, "0044", StringComparison.OrdinalIgnoreCase);
        }

        private bool ShouldSuppressAsDuplicate(ChatLogItem chatLogItem)
        {
            if (chatLogItem == null)
            {
                return true;
            }

            var normalizedLine = (chatLogItem.Line ?? string.Empty).Trim();
            if (normalizedLine.Length == 0)
            {
                return true;
            }

            var signature = string.Concat(chatLogItem.Code ?? string.Empty, "|", normalizedLine);
            var now = DateTime.UtcNow;
            if (_recentEmittedMessages.TryGetValue(signature, out var previousEmittedAt))
            {
                if ((now - previousEmittedAt) <= DuplicateSuppressionWindow)
                {
                    return true;
                }
            }

            _recentEmittedMessages[signature] = now;
            return false;
        }

        private void ChatMessageEvetRiser()
        {
            Task.Factory.StartNew(async () =>
            {
                if (_FFChatMessageArrived.HandlersCount == 0)
                {
                    while (_keepWorking && _FFChatMessageArrived.HandlersCount == 0)
                    {
                        await Task.Delay(50);
                    }
                }

                while (_keepWorking)
                {
                    try
                    {
                        if (_ffxivChat.TryDequeue(out var ffChatMsg))
                        {
                            var ea = new ChatMessageArrivedEventArgs(this) { ChatMessage = ffChatMsg };

                            await _FFChatMessageArrived.InvokeAsync(ea);
                        }
                        else
                        {
                            await Task.Delay(10);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        _logger.WriteLog("FFMemoryReader.ChatMessageEvetRiser canceled.");
                        break;
                    }
                    catch (Exception e)
                    {
                        _logger.WriteLog("FFMemoryReader.ChatMessageEvetRiser failed.");
                        _logger.WriteLog(e);
                    }
                }
            }, TaskCreationOptions.LongRunning);
        }

        private void ProcessChatMsg(ChatLogItem chatLogItem)
        {
            if (ShouldSuppressAsDuplicate(chatLogItem))
            {
                return;
            }

            var tmpMsg = new FFChatMsg(chatLogItem.Line, chatLogItem.Code, chatLogItem.TimeStamp);
            _ffxivChat.Enqueue(tmpMsg);
        }

        private void OnPropertyChanged([CallerMemberName] string prop = "")
        {
            var eventArgs = new AsyncPropertyChangedEventArgs(this, prop);
            _AsyncPropertyChanged.InvokeAsync(eventArgs).Forget();
        }

        private void EventErrorHandler(string eventName, Exception ex)
        {
            var text = eventName + Environment.NewLine + Convert.ToString(ex);
            _logger.WriteLog(text);
        }

        public void Stop()
        {
            _keepWorking = false;
            _keepReading = false;
        }

        public void Dispose()
        {
            if (_ffXivProcess != null)
                _ffXivProcess.Dispose();
        }
    }
}
