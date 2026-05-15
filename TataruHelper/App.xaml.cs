// This is an open source non-commercial project. Dear PVS-Studio, please check it.
// PVS-Studio Static Code Analyzer for C, C++, C#, and Java: http://www.viva64.com

using System;
using System.Windows;
using FFXIVTataruHelper.Services.Logging;
using Microsoft.Extensions.DependencyInjection;

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
            this.Dispatcher.UnhandledException += OnDispatcherUnhandledException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

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

        void OnDispatcherUnhandledException(object sender, System.Windows.Threading.DispatcherUnhandledExceptionEventArgs e)
        {
            string errorMessage = string.Format("An unhandled exception occurred: {0}", e.Exception.Message);

            var logger = _serviceProvider?.GetService<IAppLogger>();
            if (logger != null)
            {
                logger.WriteLog(errorMessage);
                logger.WriteLog(Convert.ToString(e.Exception));
            }
            else
            {
                Logger.WriteLog(errorMessage);
                Logger.WriteLog(Convert.ToString(e.Exception));
            }
            e.Handled = true;
        }
    }
}
