using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using FFXIVTataruHelper.Services.Logging;

namespace FFXIVTataruHelper.Services.Settings
{
    /// <summary>
    /// Puts the application back to what a fresh installation would have.
    ///
    /// By taking the saved settings away rather than by writing defaults over
    /// them: the one place that knows what a complete set looks like is the
    /// load, which fills in a chat window when there are none, adds the chat
    /// codes a build has learned since, and numbers the windows. Anything that
    /// built its own idea of "default" here would drift from that.
    /// </summary>
    public interface ISettingsResetService
    {
        /// <summary>
        /// Whether the settings have been taken away and must not be written
        /// back. The application saves on its way out, and that save would
        /// undo the whole thing.
        /// </summary>
        bool WasReset { get; }

        /// <summary>What a reset would remove, for telling the user before it does.</summary>
        IReadOnlyList<string> Removes { get; }

        Task ResetAsync();
    }

    public sealed class SettingsResetService : ISettingsResetService
    {
        private static readonly TimeSpan SyncShutdownTimeout = TimeSpan.FromSeconds(5);

        private readonly ISettingsStore _settingsStore;
        private readonly ISettingsSyncService _settingsSyncService;
        private readonly IAppLogger _logger;

        public SettingsResetService(
            ISettingsStore settingsStore, ISettingsSyncService settingsSyncService, IAppLogger logger)
        {
            _settingsStore = settingsStore;
            _settingsSyncService = settingsSyncService;
            _logger = logger;
        }

        public bool WasReset { get; private set; }

        /// <summary>
        /// The user's own settings, and the older file they may have been
        /// migrated from.
        ///
        /// Not the API keys, which are credentials somebody typed in and would
        /// have to find again, and not the system settings, which are paths and
        /// timeouts rather than anything chosen.
        /// </summary>
        public IReadOnlyList<string> Removes => new[]
        {
            _settingsStore.SettingsPath,
            _settingsStore.OldSettingsPath
        };

        public async Task ResetAsync()
        {
            // Stopped first, and it writes out anything pending as it goes.
            // Deleting while it still watches would have the next change put
            // the file straight back.
            using (var cancellation = new CancellationTokenSource(SyncShutdownTimeout))
            {
                try
                {
                    await _settingsSyncService.StopAsync(cancellation.Token).ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    _logger.WriteLog("Settings reset could not stop the settings sync.");
                    _logger.WriteLog(ex);
                }
            }

            foreach (var path in Removes)
            {
                Delete(path);
            }

            // Set last: whatever happened above, the application must not save
            // the settings it still holds in memory on its way out.
            WasReset = true;
        }

        private void Delete(string path)
        {
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path))
                {
                    return;
                }

                File.Delete(path);
                _logger.WriteLog("Settings reset removed " + path + ".");
            }
            catch (Exception ex)
            {
                // One file refusing to go is not a reason to keep the others.
                _logger.WriteLog("Settings reset could not remove " + path + ".");
                _logger.WriteLog(ex);
            }
        }
    }
}
