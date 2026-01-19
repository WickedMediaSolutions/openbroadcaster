using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster
{
    public partial class App : System.Windows.Application
    {
        private static readonly object CrashSync = new();
        private static bool _crashReported;

        public App()
        {
            try
            {
                InitializeComponent();
            }
            catch (Exception ex)
            {
                ReportCrash("Application XAML", ex, isFatal: true);
                Shutdown(-1);
                throw;
            }

            DispatcherUnhandledException += OnDispatcherUnhandledException;
            AppDomain.CurrentDomain.UnhandledException += OnCurrentDomainUnhandledException;
            TaskScheduler.UnobservedTaskException += OnUnobservedTaskException;
        }

        protected override void OnStartup(StartupEventArgs e)
        {
            AppLogger.Configure();
            base.OnStartup(e);

            try
            {
                var window = new MainWindow();
                MainWindow = window;
                window.Show();
            }
            catch (Exception ex)
            {
                ReportCrash("Startup", ex, isFatal: true);
                Shutdown(-1);
            }
        }

        protected override void OnExit(ExitEventArgs e)
        {
            base.OnExit(e);
            AppLogger.Shutdown();
        }

        private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        {
            ReportCrash("UI Dispatcher", e.Exception, isFatal: true);
            e.Handled = true;
            Shutdown();
        }

        private void OnCurrentDomainUnhandledException(object? sender, UnhandledExceptionEventArgs e)
        {
            var exception = e.ExceptionObject as Exception ?? new Exception(e.ExceptionObject?.ToString() ?? "Unknown application error");
            ReportCrash("AppDomain", exception, isFatal: true);
        }

        private void OnUnobservedTaskException(object? sender, UnobservedTaskExceptionEventArgs e)
        {
            ReportCrash("Background Task", e.Exception, isFatal: false);
            e.SetObserved();
        }

        private static void ReportCrash(string source, Exception? exception, bool isFatal)
        {
            if (exception == null)
            {
                return;
            }

            var logger = AppLogger.CreateLogger<App>();
            logger.LogError(exception, "Unhandled exception captured via {Source} (Fatal={Fatal})", source, isFatal);

            if (!isFatal)
            {
                return;
            }

            lock (CrashSync)
            {
                if (_crashReported)
                {
                    return;
                }

                _crashReported = true;
            }

            var logPath = AppLogger.CurrentLogFile ?? AppLogger.LogDirectory;
            var message = $"OpenBroadcaster encountered an unexpected error and needs to close.\n\nSource: {source}\nDetails: {exception.Message}\n\nSee log for details:\n{logPath}";

            try
            {
                System.Windows.MessageBox.Show(message, "OpenBroadcaster Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            catch
            {
                // Swallow UI issues while already shutting down.
            }
        }
    }
}
