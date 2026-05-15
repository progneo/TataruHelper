using System;
using System.Threading;
using System.Threading.Tasks;
using Updater.EventArguments;
using Velopack;
using Velopack.Sources;

namespace Updater
{
    public sealed class VelopackUpdateService : IUpdateService
    {
        private readonly ILog _logger;
        private readonly string _repositoryUrl;
        private readonly SemaphoreSlim _updateLock;
        private readonly object _stateSync;
        private readonly AsyncEvent<UpdateStateChangedEventArgs> _updateStateChanged;

        private CancellationTokenSource _activeUpdateCts;
        private VelopackAsset _pendingUpdate;
        private string _lastExplicitChannel;
        private bool _disposed;

        public event AsyncEventHandler<UpdateStateChangedEventArgs> UpdateStateChanged
        {
            add { _updateStateChanged.Register(value); }
            remove { _updateStateChanged.Unregister(value); }
        }

        public bool IsUpdating { get; private set; }

        public VelopackUpdateService(ILog logger, string repositoryUrl = "https://github.com/progneo/TataruHelper")
        {
            _logger = logger;
            _repositoryUrl = repositoryUrl;
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
                _logger?.WriteLog("Update check skipped: update is already in progress.");
                return;
            }

            CancellationTokenSource linkedTokenSource = null;
            try
            {
                IsUpdating = true;

                linkedTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                lock (_stateSync)
                {
                    if (_activeUpdateCts != null)
                    {
                        _activeUpdateCts.Dispose();
                    }

                    _activeUpdateCts = linkedTokenSource;
                    _pendingUpdate = null;
                }

                await PublishStateAsync(UpdateState.Initializing).ConfigureAwait(false);

                var explicitChannel = UpdateChannelResolver.ResolveExplicitChannel(prerelease);
                var source = new GithubSource(_repositoryUrl, null, prerelease);
                var options = new UpdateOptions()
                {
                    ExplicitChannel = explicitChannel
                };

                var updateManager = new UpdateManager(source, options);
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
                await updateManager.DownloadUpdatesAsync(updateInfo, null, linkedTokenSource.Token).ConfigureAwait(false);

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
                _logger?.WriteLog(ex.ToString());
                await PublishStateAsync(UpdateState.Error, "Update check failed.", ex).ConfigureAwait(false);
            }
            finally
            {
                lock (_stateSync)
                {
                    if (_activeUpdateCts == linkedTokenSource)
                    {
                        _activeUpdateCts = null;
                    }
                }

                IsUpdating = false;
                _updateLock.Release();

                if (linkedTokenSource != null)
                {
                    linkedTokenSource.Dispose();
                }
            }
        }

        public void RestartApp()
        {
            ThrowIfDisposed();

            try
            {
                var source = new GithubSource(_repositoryUrl, null, false);
                var options = new UpdateOptions()
                {
                    ExplicitChannel = _lastExplicitChannel
                };

                var updateManager = new UpdateManager(source, options);
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
                    _logger?.WriteLog("Restart requested but no pending update was found.");
                }
            }
            catch (Exception ex)
            {
                _logger?.WriteLog(ex.ToString());
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
            if (_disposed)
                return;

            _disposed = true;
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
            _logger?.WriteLog(eventName + Environment.NewLine + ex);
        }

        private void ThrowIfDisposed()
        {
            if (_disposed)
            {
                throw new ObjectDisposedException(nameof(VelopackUpdateService));
            }
        }
    }
}
