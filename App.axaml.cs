using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using OpenBroadcaster.Core.Diagnostics;

namespace OpenBroadcaster
{
    public sealed partial class App : Application
    {
        private static readonly object CrashSync = new();
        private static bool _crashReported;

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            AppLogger.Configure();

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                var window = new MainWindow();
                desktop.MainWindow = window;
                UiServices.MainWindow = window;
            }

            base.OnFrameworkInitializationCompleted();
        }

        public static void ReportCrash(string source, System.Exception? exception, bool isFatal)
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

            Dispatcher.UIThread.Post(async () =>
            {
                await UiServices.ShowErrorAsync("OpenBroadcaster Error", message);
            });
        }
    }
}
