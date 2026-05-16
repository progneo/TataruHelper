using System;
using System.Threading;
using System.Threading.Tasks;
using Updater;
using Updater.EventArguments;

namespace FFXIVTataruHelper.Services.Update
{
    public sealed class DisabledUpdateService : IUpdateService
    {
        public event Updater.AsyncEventHandler<UpdateStateChangedEventArgs> UpdateStateChanged
        {
            add { }
            remove { }
        }

        public bool IsUpdating => false;

        public Task CheckAndInstallUpdatesAsync(bool prerelease, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }

        public void RestartApp()
        {
        }

        public void StopUpdate()
        {
        }

        public void Dispose()
        {
        }
    }
}
