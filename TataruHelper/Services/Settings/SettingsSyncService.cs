using FFXIVTataruHelper.Services.Logging;
using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace FFXIVTataruHelper.Services.Settings
{
    public sealed class SettingsSyncService : ISettingsSyncService
    {
        private readonly ISettingsStore _settingsStore;
        private readonly IAppLogger _logger;
        private readonly object _sync = new object();

        private INotifyPropertyChanged _settingsSource;
        private Func<Task> _persistSettingsAsync;
        private CancellationTokenSource _workerCancellation;
        private Task _workerTask = Task.CompletedTask;
        private int _isDirty;
        private bool _isStarted;

        public SettingsSyncService(ISettingsStore settingsStore, IAppLogger logger)
        {
            _settingsStore = settingsStore;
            _logger = logger;
        }

        public void Start(INotifyPropertyChanged settingsSource, Func<Task> persistSettingsAsync)
        {
            if (settingsSource == null)
            {
                throw new ArgumentNullException(nameof(settingsSource));
            }

            if (persistSettingsAsync == null)
            {
                throw new ArgumentNullException(nameof(persistSettingsAsync));
            }

            lock (_sync)
            {
                if (_isStarted)
                {
                    throw new InvalidOperationException("Settings synchronization service is already started.");
                }

                _settingsSource = settingsSource;
                _persistSettingsAsync = persistSettingsAsync;
                _workerCancellation = new CancellationTokenSource();

                _settingsSource.PropertyChanged += OnSettingsChanged;
                _workerTask = Task.Run(() => WatchChangesAsync(_workerCancellation.Token));
                _isStarted = true;
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken = default)
        {
            Task workerTask;
            Func<Task> persistSettingsAsync;
            bool hasPendingChanges;
            lock (_sync)
            {
                if (!_isStarted)
                {
                    return;
                }

                _isStarted = false;
                _settingsSource.PropertyChanged -= OnSettingsChanged;
                persistSettingsAsync = _persistSettingsAsync;
                hasPendingChanges = Interlocked.CompareExchange(ref _isDirty, 0, 0) != 0;
                _workerCancellation.Cancel();
                workerTask = _workerTask;
            }

            try
            {
                await workerTask.WaitAsync(cancellationToken);

                if (hasPendingChanges)
                {
                    await persistSettingsAsync();
                }
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                _logger.WriteLog("Settings sync worker stopped with error.");
                _logger.WriteLog(ex);
            }
            finally
            {
                lock (_sync)
                {
                    _workerCancellation.Dispose();
                    _workerCancellation = null;
                    _settingsSource = null;
                    _persistSettingsAsync = null;
                    _isDirty = 0;
                    _workerTask = Task.CompletedTask;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                StopAsync().GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void OnSettingsChanged(object sender, PropertyChangedEventArgs e)
        {
            Interlocked.Exchange(ref _isDirty, 1);
        }

        private async Task WatchChangesAsync(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    await Task.Delay(_settingsStore.SettingsSaveDelayMs, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                if (Interlocked.Exchange(ref _isDirty, 0) == 0)
                {
                    continue;
                }

                try
                {
                    await _persistSettingsAsync();
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    _logger.WriteLog("Failed to persist user settings in settings sync loop.");
                    _logger.WriteLog(ex);
                }
            }
        }
    }
}
