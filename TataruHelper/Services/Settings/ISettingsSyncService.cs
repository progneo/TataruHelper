using System;
using System.ComponentModel;
using System.Threading;
using System.Threading.Tasks;

namespace FFXIVTataruHelper.Services.Settings
{
    public interface ISettingsSyncService : IDisposable
    {
        void Start(INotifyPropertyChanged settingsSource, Func<Task> persistSettingsAsync);

        Task StopAsync(CancellationToken cancellationToken = default);
    }
}
