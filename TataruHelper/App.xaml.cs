// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Security.Principal;
using System.Windows;
using System.Windows.Threading;

using FFXIVTataruHelper.Services.Logging;
using FFXIVTataruHelper.Theme;

using Microsoft.Extensions.DependencyInjection;

using Velopack;

namespace FFXIVTataruHelper
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        private IServiceProvider _serviceProvider;

        public App()
        {
            VelopackApp.Build().Run();
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            if (ShouldSkipVelopackFirstRunLaunch())
            {
                Shutdown();
                return;
            }

            if (ShouldElevateAfterVelopackInstall() && !TryRelaunchAsAdministrator(e))
            {
                Shutdown();
                return;
            }

            base.OnStartup(e);

            Logger.RawDialogLogEnabled = ShouldEnableRawDialogLog(e.Args);

            ShutdownMode = ShutdownMode.OnMainWindowClose;

            try
            {
                AppThemeService.Apply(AppThemeMode.System);
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine(ex);
            }

            _serviceProvider = AppCompositionRoot.BuildServiceProvider();
            var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
            MainWindow = mainWindow;
            mainWindow.Show();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            if (_serviceProvider is IDisposable disposable)
            {
                disposable.Dispose();
            }

            base.OnExit(e);
        }

        void OnDispatcherUnhandledException(object sender,
            DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("An unhandled exception occurred: {0}", e.Exception.Message);

            var logger = _serviceProvider?.GetService<IAppLogger>();
            if (logger != null)
            {
                logger.WriteLog(errorMessage);
                logger.WriteLog(Convert.ToString(e.Exception));
            }

            e.Handled = true;
        }

        internal static bool ShouldEnableRawDialogLog(string[] args)
        {
            if (args == null)
            {
                return false;
            }

            foreach (var arg in args)
            {
                if (string.IsNullOrWhiteSpace(arg))
                {
                    continue;
                }

                var normalized = arg.Trim().TrimStart('-', '/');
                if (string.Equals(normalized, "log-raw-dialog", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ShouldElevateAfterVelopackInstall()
        {
            var isFirstRun = string.Equals(
                Environment.GetEnvironmentVariable("VELOPACK_FIRSTRUN"),
                "true",
                StringComparison.OrdinalIgnoreCase);

            return isFirstRun && OperatingSystem.IsWindows() && !IsRunningAsAdministrator();
        }

        private static bool ShouldSkipVelopackFirstRunLaunch()
        {
            return string.Equals(
                Environment.GetEnvironmentVariable("VELOPACK_FIRSTRUN"),
                "true",
                StringComparison.OrdinalIgnoreCase);
        }

        [SupportedOSPlatform("windows")]
        private static bool IsRunningAsAdministrator()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }

        private static bool TryRelaunchAsAdministrator(StartupEventArgs e)
        {
            var executablePath = Environment.ProcessPath;
            if (string.IsNullOrWhiteSpace(executablePath))
                return false;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = executablePath, UseShellExecute = true, Verb = "runas"
                };

                foreach (var arg in e.Args)
                {
                    startInfo.ArgumentList.Add(arg);
                }

                Process.Start(startInfo);

                return true;
            }
            catch (Win32Exception)
            {
                // User canceled UAC prompt.
                return false;
            }
        }
    }
}