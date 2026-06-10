using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

using Microsoft.Extensions.Logging.Abstractions;

using NUnit.Framework;

using Updater.EventArguments;

using Velopack;

namespace Updater.Tests
{
    [TestFixture]
    public class VelopackUpdateServiceStateMachineTests
    {
        private sealed class FakeUpdateManager : IUpdateManagerAdapter
        {
            public bool IsInstalled { get; set; } = true;

            public VelopackAsset UpdatePendingRestart { get; set; }

            public UpdateInfo UpdateInfoResult { get; set; }

            public Func<CancellationToken, Task> DownloadCallback { get; set; }

            public VelopackAsset AppliedUpdate { get; private set; }

            public Task<UpdateInfo> CheckForUpdatesAsync()
            {
                return Task.FromResult(UpdateInfoResult);
            }

            public Task DownloadUpdatesAsync(UpdateInfo updateInfo, CancellationToken cancellationToken)
            {
                return DownloadCallback != null ? DownloadCallback(cancellationToken) : Task.CompletedTask;
            }

            public void ApplyUpdatesAndRestart(VelopackAsset update)
            {
                AppliedUpdate = update;
            }
        }

        private static VelopackUpdateService CreateService(FakeUpdateManager updateManager,
            out List<UpdateState> states)
        {
            var service = new VelopackUpdateService(NullLogger.Instance,
                updateManagerFactory: (url, prerelease, channel) => updateManager);

            var observed = new List<UpdateState>();
            service.UpdateStateChanged += ea =>
            {
                lock (observed)
                {
                    observed.Add(ea.State);
                }

                return Task.CompletedTask;
            };

            states = observed;
            return service;
        }

        [Test]
        public async Task CheckAndInstall_NotInstalled_PublishesError()
        {
            var manager = new FakeUpdateManager { IsInstalled = false };

            using (var service = CreateService(manager, out var states))
            {
                await service.CheckAndInstallUpdatesAsync(false, CancellationToken.None);

                Assert.That(states, Is.EqualTo(new[] { UpdateState.Initializing, UpdateState.Error }));
                Assert.That(service.IsUpdating, Is.False);
            }
        }

        [Test]
        public async Task CheckAndInstall_NoUpdates_PublishesNoUpdatesFoundThenFinished()
        {
            var manager = new FakeUpdateManager { UpdateInfoResult = null };

            using (var service = CreateService(manager, out var states))
            {
                await service.CheckAndInstallUpdatesAsync(false, CancellationToken.None);

                Assert.That(states, Is.EqualTo(new[]
                {
                    UpdateState.Initializing,
                    UpdateState.LookingForUpdates,
                    UpdateState.NoUpdatesFound,
                    UpdateState.Finished,
                }));
            }
        }

        [Test]
        public async Task CheckAndInstall_UpdateAvailable_DownloadsAndReportsReadyToRestart()
        {
            var asset = new VelopackAsset();
            var manager = new FakeUpdateManager { UpdateInfoResult = new UpdateInfo(asset, false) };

            using (var service = CreateService(manager, out var states))
            {
                await service.CheckAndInstallUpdatesAsync(false, CancellationToken.None);

                Assert.That(states, Is.EqualTo(new[]
                {
                    UpdateState.Initializing,
                    UpdateState.LookingForUpdates,
                    UpdateState.Downloading,
                    UpdateState.ReadyToRestart,
                    UpdateState.Finished,
                }));

                service.RestartApp();

                Assert.That(manager.AppliedUpdate, Is.SameAs(asset));
            }
        }

        [Test]
        public async Task CheckAndInstall_StopUpdateDuringDownload_PublishesError()
        {
            var asset = new VelopackAsset();
            var downloadStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var manager = new FakeUpdateManager
            {
                UpdateInfoResult = new UpdateInfo(asset, false),
                DownloadCallback = async token =>
                {
                    downloadStarted.SetResult(true);
                    await Task.Delay(Timeout.Infinite, token);
                },
            };

            using (var service = CreateService(manager, out var states))
            {
                var updateTask = service.CheckAndInstallUpdatesAsync(false, CancellationToken.None);
                await downloadStarted.Task;

                service.StopUpdate();
                await updateTask;

                Assert.That(states, Is.EqualTo(new[]
                {
                    UpdateState.Initializing,
                    UpdateState.LookingForUpdates,
                    UpdateState.Downloading,
                    UpdateState.Error,
                }));
                Assert.That(service.IsUpdating, Is.False);
            }
        }

        [Test]
        public async Task CheckAndInstall_CheckThrows_PublishesError()
        {
            var manager = new FakeUpdateManager { UpdateInfoResult = new UpdateInfo(new VelopackAsset(), false) };
            manager.DownloadCallback = _ => throw new InvalidOperationException("download failed");

            using (var service = CreateService(manager, out var states))
            {
                await service.CheckAndInstallUpdatesAsync(false, CancellationToken.None);

                Assert.That(states[states.Count - 1], Is.EqualTo(UpdateState.Error));
                Assert.That(service.IsUpdating, Is.False);
            }
        }

        [Test]
        public void RestartApp_NoPendingUpdate_DoesNotApply()
        {
            var manager = new FakeUpdateManager();

            using (var service = CreateService(manager, out _))
            {
                service.RestartApp();

                Assert.That(manager.AppliedUpdate, Is.Null);
            }
        }
    }
}
