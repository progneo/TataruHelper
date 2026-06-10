using System.Threading;
using System.Threading.Tasks;

using Velopack;

namespace Updater
{
    public interface IUpdateManagerAdapter
    {
        bool IsInstalled { get; }

        VelopackAsset UpdatePendingRestart { get; }

        Task<UpdateInfo> CheckForUpdatesAsync();

        Task DownloadUpdatesAsync(UpdateInfo updateInfo, CancellationToken cancellationToken);

        void ApplyUpdatesAndRestart(VelopackAsset update);
    }
}