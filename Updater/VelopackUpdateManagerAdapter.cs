using System.Threading;
using System.Threading.Tasks;

using Velopack;
using Velopack.Sources;

namespace Updater
{
    public sealed class VelopackUpdateManagerAdapter : IUpdateManagerAdapter
    {
        private readonly UpdateManager _updateManager;

        public VelopackUpdateManagerAdapter(string repositoryUrl, bool prerelease, string explicitChannel)
        {
            var source = new GithubSource(repositoryUrl, null, prerelease);
            var options = new UpdateOptions()
            {
                ExplicitChannel = explicitChannel
            };

            _updateManager = new UpdateManager(source, options);
        }

        public bool IsInstalled
        {
            get { return _updateManager.IsInstalled; }
        }

        public VelopackAsset UpdatePendingRestart
        {
            get { return _updateManager.UpdatePendingRestart; }
        }

        public Task<UpdateInfo> CheckForUpdatesAsync()
        {
            return _updateManager.CheckForUpdatesAsync();
        }

        public Task DownloadUpdatesAsync(UpdateInfo updateInfo, CancellationToken cancellationToken)
        {
            return _updateManager.DownloadUpdatesAsync(updateInfo, null, cancellationToken);
        }

        public void ApplyUpdatesAndRestart(VelopackAsset update)
        {
            _updateManager.ApplyUpdatesAndRestart(update);
        }
    }
}
