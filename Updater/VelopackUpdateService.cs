using System;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging;
using Updater.EventArguments;
using Velopack;

namespace Updater
{
    public sealed class VelopackUpdateService : IUpdateService
    {
        public delegate IUpdateManagerAdapter UpdateManagerFactory(string repositoryUrl, bool prerelease,
            string explicitChannel);

        private readonly ILogger _logger;
        private readonly string _repositoryUrl;
        private readonly UpdateManagerFactory _updateManagerFactory;
        private readonly SemaphoreSlim _updateLock;
        private readonly object _stateSync;
        private readonly AsyncEvent<UpdateStateChangedEventArgs> _updateStateChanged;

        private CancellationTokenSource _activeUpdateCts;
        private VelopackAsset _pendingUpdate;
        private string _lastExplicitChannel;
        private bool _isUpdating;
        private int _disposed;

        public event AsyncEventHandler<UpdateStateChangedEventArgs> UpdateStateChanged
        {
            add { _updateStateChanged.Register(value); }
            remove { _updateStateChanged.Unregister(value); }
        }

        public bool IsUpdating
        {
            get
            {
                lock (_stateSync)
                {
                    return _isUpdating;
                }
            }
        }

        public VelopackUpdateService(ILogger logger, string repositoryUrl = "https://github.com/progneo/TataruHelper",
            UpdateManagerFactory updateManagerFactory = null)
        {
            _logger = logger;
            _repositoryUrl = repositoryUrl;
            _updateManagerFactory = updateManagerFactory ??
                ((url, prerelease, explicitChannel) => new VelopackUpdateManagerAdapter(url, prerelease, explicitChannel));
            _updateLock = new SemaphoreSlim(1, 1);
            _stateSync = new object();
            _updateStateChanged = new AsyncEvent<UpdateStateChangedEventArgs>(OnEventError, "UpdateStateChanged");
            _lastExplicitChannel = UpdateChannelResolver.ResolveExplicitChannel(false);
        }

        public async Task CheckAndInstallUpdatesAsync(bool prerelease, CancellationToken cancellationToken)
        {
            ThrowIfDisposed();

            if (!await _updateLock.WaitAsync(0, cancellationToken).ConfigureAwait(false))
            {
                _logger?.LogInformation("Update check skipped: update is already in progress.");
                return;
            }

            CancellationTokenSource linkedTokenSource = null;
            try
            {
                linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                lock (_stateSync)
                {
                    _isUpdating = true;

                    if (_activeUpdateCts != null)
                    {
                        _activeUpdateCts.Dispose();
                    }

                    _activeUpdateCts = linkedTokenSource;
                    _pendingUpdate = null;
                }

                await PublishStateAsync(UpdateState.Initializing).ConfigureAwait(false);

                var explicitChannel = UpdateChannelResolver.ResolveExplicitChannel(prerelease);
                var updateManager = _updateManagerFactory(_repositoryUrl, prerelease, explicitChannel);
                if (!updateManager.IsInstalled)
                {
                    await PublishStateAsync(UpdateState.Error, "Updater is unavailable for a non-installed build.").ConfigureAwait(false);
                    return;
                }

                await PublishStateAsync(UpdateState.LookingForUpdates).ConfigureAwait(false);
                var updateInfo = await updateManager.CheckForUpdatesAsync().ConfigureAwait(false);

                if (updateInfo == null)
                {
                    await PublishStateAsync(UpdateState.NoUpdatesFound).ConfigureAwait(false);
                    await PublishStateAsync(UpdateState.Finished).ConfigureAwait(false);
                    return;
                }

                await PublishStateAsync(UpdateState.Downloading).ConfigureAwait(false);
                await updateManager.DownloadUpdatesAsync(updateInfo, linkedTokenSource.Token).ConfigureAwait(false);

                lock (_stateSync)
                {
                    _lastExplicitChannel = explicitChannel;
                    _pendingUpdate = updateInfo.TargetFullRelease;
                }

                await PublishStateAsync(UpdateState.ReadyToRestart).ConfigureAwait(false);
                await PublishStateAsync(UpdateState.Finished).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                await PublishStateAsync(UpdateState.Error, "Update check canceled.").ConfigureAwait(false);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Update operation failed.");
                await PublishStateAsync(UpdateState.Error, "Update check failed.", ex).ConfigureAwait(false);
            }
            finally
            {
                // Clearing and disposing the CTS under the same lock used by
                // StopUpdate guarantees Cancel can never hit a disposed source.
                lock (_stateSync)
                {
                    if (_activeUpdateCts == linkedTokenSource)
                    {
                        _activeUpdateCts = null;
                    }

                    _isUpdating = false;

                    if (linkedTokenSource != null)
                    {
                        linkedTokenSource.Dispose();
                    }
                }

                _updateLock.Release();
            }
        }

        public void RestartApp()
        {
            ThrowIfDisposed();

            try
            {
                string explicitChannel;
                lock (_stateSync)
                {
                    explicitChannel = _lastExplicitChannel;
                }

                var updateManager = _updateManagerFactory(_repositoryUrl, false, explicitChannel);
                VelopackAsset pendingUpdate = null;
                lock (_stateSync)
                {
                    pendingUpdate = _pendingUpdate ?? updateManager.UpdatePendingRestart;
                }

                if (pendingUpdate != null)
                {
                    updateManager.ApplyUpdatesAndRestart(pendingUpdate);
                }
                else
                {
                    _logger?.LogInformation("Restart requested but no pending update was found.");
                }
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Update operation failed.");
            }
        }

        public void StopUpdate()
        {
            lock (_stateSync)
            {
                if (_activeUpdateCts != null && !_activeUpdateCts.IsCancellationRequested)
                {
                    _activeUpdateCts.Cancel();
                }
            }
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            StopUpdate();

            lock (_stateSync)
            {
                if (_activeUpdateCts != null)
                {
                    _activeUpdateCts.Dispose();
                    _activeUpdateCts = null;
                }
            }

            _updateLock.Dispose();
        }

        private async Task PublishStateAsync(UpdateState state, string text = "", Exception error = null)
        {
            await _updateStateChanged.InvokeAsync(new UpdateStateChangedEventArgs(state, text, error)).ConfigureAwait(false);
        }

        private void OnEventError(string eventName, Exception ex)
        {
            _logger?.LogError(ex, "Unhandled exception in {EventName} handler.", eventName);
        }

        private void ThrowIfDisposed()
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                throw new ObjectDisposedException(nameof(VelopackUpdateService));
            }
        }
    }
}
