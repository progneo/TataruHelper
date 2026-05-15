using System;
using System.Threading;
using System.Threading.Tasks;
using Updater.EventArguments;

namespace Updater
{
    public interface IUpdateService : IDisposable
    {
        event AsyncEventHandler<UpdateStateChangedEventArgs> UpdateStateChanged;

        bool IsUpdating { get; }

        Task CheckAndInstallUpdatesAsync(bool prerelease, CancellationToken cancellationToken);

        void RestartApp();

        void StopUpdate();
    }
}
