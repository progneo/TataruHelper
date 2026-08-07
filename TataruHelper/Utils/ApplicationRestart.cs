using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;

using FFXIVTataruHelper.Services.Logging;

namespace FFXIVTataruHelper.Utils
{
    /// <summary>
    /// Starts the application again once this copy has gone.
    ///
    /// Only one copy may run - a named mutex sees to that - so a new one
    /// started while this is still closing would find the mutex taken, tell the
    /// old window to show itself, and quietly exit. It is therefore asked to
    /// wait for this process to end before it looks.
    /// </summary>
    public static class ApplicationRestart
    {
        private const string WaitSwitch = "--wait-for-exit";

        private static readonly TimeSpan LongestWait = TimeSpan.FromSeconds(20);

        /// <summary>
        /// Launches the copy that will take over. Call it just before shutting
        /// down; it does not wait for anything itself.
        /// </summary>
        public static bool Start(IReadOnlyList<string> currentArguments, IAppLogger logger)
        {
            var executable = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executable))
            {
                logger?.WriteLog("Cannot restart: the running executable has no path.");
                return false;
            }

            var startInfo = new ProcessStartInfo { FileName = executable, UseShellExecute = false };

            startInfo.ArgumentList.Add(WaitSwitch);
            startInfo.ArgumentList.Add(Environment.ProcessId.ToString(CultureInfo.InvariantCulture));

            foreach (var argument in Carry(currentArguments))
            {
                startInfo.ArgumentList.Add(argument);
            }

            try
            {
                // Started rather than shell-executed: this process is elevated,
                // so the one it starts is too, and nobody is asked about it a
                // second time.
                Process.Start(startInfo);
                return true;
            }
            catch (Exception ex)
            {
                logger?.WriteLog("Cannot restart the application.");
                logger?.WriteLog(ex);
                return false;
            }
        }

        /// <summary>
        /// The arguments worth carrying over: everything except a wait from a
        /// restart that has already happened.
        /// </summary>
        internal static IEnumerable<string> Carry(IReadOnlyList<string> arguments)
        {
            if (arguments == null)
            {
                yield break;
            }

            for (var i = 0; i < arguments.Count; i++)
            {
                if (IsWaitSwitch(arguments[i]))
                {
                    i++;
                    continue;
                }

                yield return arguments[i];
            }
        }

        /// <summary>
        /// The process this copy was asked to wait for, or zero. Read before
        /// anything else looks at the mutex.
        /// </summary>
        internal static int WaitsFor(IReadOnlyList<string> arguments)
        {
            if (arguments == null)
            {
                return 0;
            }

            for (var i = 0; i < arguments.Count - 1; i++)
            {
                if (IsWaitSwitch(arguments[i]) &&
                    int.TryParse(arguments[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) &&
                    id > 0)
                {
                    return id;
                }
            }

            return 0;
        }

        /// <summary>
        /// Waits for the copy that started this one to finish going, so the
        /// mutex it holds is free by the time anything asks for it.
        /// </summary>
        public static void WaitForPrevious(IReadOnlyList<string> arguments, IAppLogger logger)
        {
            var id = WaitsFor(arguments);
            if (id == 0)
            {
                return;
            }

            try
            {
                using var previous = Process.GetProcessById(id);
                if (!previous.WaitForExit(LongestWait))
                {
                    // Carrying on is better than not starting at all: the
                    // single-instance check will simply show that window.
                    logger?.WriteLog("The previous copy is still running after " + LongestWait + "; carrying on.");
                }
            }
            catch (ArgumentException)
            {
                // Already gone, which is what was being waited for.
            }
            catch (Exception ex)
            {
                logger?.WriteLog(ex);
            }
        }

        private static bool IsWaitSwitch(string argument)
        {
            return string.Equals(
                (argument ?? string.Empty).Trim().TrimStart('-', '/'),
                WaitSwitch.TrimStart('-'),
                StringComparison.OrdinalIgnoreCase);
        }
    }
}
