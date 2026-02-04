using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using System.Linq;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Avalonia.ViewModels;
using OpenBroadcaster.Avalonia.Views;

namespace OpenBroadcaster.Avalonia
{
    public partial class App : Application
    {
        private Action<string>? _logToFile;
        private static OpenBroadcaster.Core.DependencyInjection.ServiceContainer? _serviceContainer;

        /// <summary>
        /// Gets the global service container.
        /// </summary>
        public static OpenBroadcaster.Core.DependencyInjection.ServiceContainer ServiceContainer
        {
            get
            {
                _serviceContainer ??= new OpenBroadcaster.Core.DependencyInjection.ServiceContainer();
                return _serviceContainer;
            }
        }

        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Configure structured logging
            var logsDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "logs");
            OpenBroadcaster.Core.Logging.LoggerFactory.Instance.Configure(logsDir, OpenBroadcaster.Core.Logging.LogLevel.Debug);

            // Write all diagnostics to a file we can read later
            string logFile = "crash.log";
            string crashDir = logsDir;
            try { System.IO.Directory.CreateDirectory(crashDir); logFile = System.IO.Path.Combine(crashDir, "crash.log"); } catch { }

            _logToFile = (string msg) =>
            {
                try { System.IO.File.AppendAllText(logFile, msg + "\n"); } catch { }
                try { System.Console.WriteLine(msg); } catch { }
            };

            try
            {
                _logToFile!($"[{DateTime.UtcNow:o}] === APP START ===");
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener("avalonia-debug.log"));
                System.Diagnostics.Trace.AutoFlush = true;

                AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                {
                    try
                    {
                        var ex = e.Exception;
                        _logToFile!($"[FirstChance] {ex.GetType()}: {ex.Message}");
                    }
                    catch { }
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try
                    {
                        var ex = e.Exception;
                        _logToFile!($"[UnobservedTask] {ex.GetType()}: {ex.Message}");
                    }
                    catch { }
                };
            }
            catch { }

            // Add global unhandled exception handler
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
            {
                try
                {
                    var ex = (Exception)e.ExceptionObject;
                    var fullMsg = $"[FATAL] {ex.GetType()}: {ex.Message}\n{ex.StackTrace}";
                    _logToFile!(fullMsg);
                    _logToFile!("\n=== ERROR LOG LOCATION ===");
                    _logToFile!($"File: {logFile}");
                    _logToFile!("\nOpen this file to see the full error details.");
                }
                catch { }
            };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    var services = InitializeServices();
                    var mainWindow = CreateAndShowMainWindow();
                    var filePicker = CreateFilePickerDelegate(mainWindow);
                    var appSettings = LoadAndConfigureSettings(services.OverlayService);
                    var viewModel = CreateMainViewModel(services, appSettings, filePicker);
                    ConfigureMainWindow(desktop, mainWindow, viewModel);
                    
                    _logToFile!("[INIT] Application startup complete!");
                }
                catch (Exception ex)
                {
                    var fullMsg = $"[INIT ERROR] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                    _logToFile!(fullMsg);
                    _logToFile!("\n=== ERROR DETAILS SAVED TO LOG FILE ===");
                    _logToFile!($"Location: {logFile}");
                    throw;
                }
            }

            base.OnFrameworkInitializationCompleted();
        }

        private ApplicationServices InitializeServices()
        {
            _logToFile!("[INIT] Creating EventBus...");
            var eventBus = new OpenBroadcaster.Core.Messaging.EventBus();
            
            _logToFile!("[INIT] Creating QueueService...");
            var queue = new OpenBroadcaster.Core.Services.QueueService();
            
            _logToFile!("[INIT] Creating AudioService...");
            var audio = new OpenBroadcaster.Core.Services.AudioService();
            
            _logToFile!("[INIT] Creating TransportService...");
            var transport = new OpenBroadcaster.Core.Services.TransportService(eventBus, queue, audio);
            
            _logToFile!("[INIT] Creating RadioService...");
            var radioService = new OpenBroadcaster.Core.Services.RadioService(transport, eventBus);
            
            _logToFile!("[INIT] Creating LibraryService...");
            var library = new OpenBroadcaster.Core.Services.LibraryService();
            
            _logToFile!("[INIT] Creating CartWallService...");
            var cartWall = new OpenBroadcaster.Core.Services.CartWallService(audio, null, 16);
            
            _logToFile!("[INIT] Creating OverlayService...");
            var overlayService = new OpenBroadcaster.Core.Overlay.OverlayService(queue, eventBus);
            overlayService.SetLibraryService(library);

            // Register all services in DI container
            _logToFile!("[INIT] Registering services in DI container...");
            ServiceContainer.RegisterSingleton(eventBus);
            ServiceContainer.RegisterSingleton(queue);
            ServiceContainer.RegisterSingleton(audio);
            ServiceContainer.RegisterSingleton(transport);
            ServiceContainer.RegisterSingleton(radioService);
            ServiceContainer.RegisterSingleton(library);
            ServiceContainer.RegisterSingleton(cartWall);
            ServiceContainer.RegisterSingleton(overlayService);

            return new ApplicationServices(eventBus, queue, audio, transport, radioService, library, cartWall, overlayService);
        }

        private MainWindow CreateAndShowMainWindow()
        {
            _logToFile!("[INIT] Creating MainWindow...");
            var mainWindow = new MainWindow();
            mainWindow.Show();
            mainWindow.Activate();
            try { mainWindow.WindowState = WindowState.Maximized; } catch { }
            return mainWindow;
        }

        private System.Func<int, System.Threading.Tasks.Task<string?>> CreateFilePickerDelegate(MainWindow mainWindow)
        {
            _logToFile!("[INIT] Setting up file picker...");
            return async (padId) =>
            {
                try
                {
                    var provider = mainWindow.StorageProvider;
                    var files = await provider.OpenFilePickerAsync(new FilePickerOpenOptions
                    {
                        Title = "Select audio file for cart pad",
                        AllowMultiple = false,
                        FileTypeFilter = new[]
                        {
                            new FilePickerFileType("Audio Files")
                            {
                                Patterns = new[] { "*.mp3", "*.wav", "*.flac", "*.ogg" }
                            },
                            new FilePickerFileType("All Files")
                            {
                                Patterns = new[] { "*" }
                            }
                        }
                    });

                    if (files != null && files.Count > 0)
                    {
                        var path = files[0].Path.LocalPath;
                        if (!string.IsNullOrWhiteSpace(path))
                        {
                            return path;
                        }
                    }
                    return null;
                }
                catch
                {
                    return null;
                }
            };
        }

        private OpenBroadcaster.Core.Models.AppSettings LoadAndConfigureSettings(OpenBroadcaster.Core.Overlay.OverlayService overlayService)
        {
            _logToFile!("[INIT] Loading app settings...");
            var settingsStore = new OpenBroadcaster.Core.Services.AppSettingsStore();
            var appSettings = settingsStore.Load();
            
            appSettings.Overlay ??= new OpenBroadcaster.Core.Models.OverlaySettings();
            if (appSettings.Overlay.Port == 0)
            {
                appSettings.Overlay.Port = 9750;
            }
            
            _logToFile!("[INIT] Configuring overlay service...");
            overlayService.UpdateSettings(appSettings.Overlay);

            return appSettings;
        }

        private MainWindowViewModel CreateMainViewModel(ApplicationServices services, OpenBroadcaster.Core.Models.AppSettings appSettings, System.Func<int, System.Threading.Tasks.Task<string?>> filePicker)
        {
            _logToFile!("[INIT] Creating MainWindowViewModel...");
            return new MainWindowViewModel(
                services.RadioService,
                services.Transport,
                services.Audio,
                services.EventBus,
                services.Queue,
                services.Library,
                services.CartWall,
                services.OverlayService,
                appSettings,
                filePicker);
        }

        private void ConfigureMainWindow(IClassicDesktopStyleApplicationLifetime desktop, MainWindow mainWindow, MainWindowViewModel viewModel)
        {
            _logToFile!("[INIT] Registering main window...");
            desktop.MainWindow = mainWindow;
            mainWindow.DataContext = viewModel;
        }

        private class ApplicationServices
        {
            public OpenBroadcaster.Core.Messaging.EventBus EventBus { get; }
            public OpenBroadcaster.Core.Services.QueueService Queue { get; }
            public OpenBroadcaster.Core.Services.AudioService Audio { get; }
            public OpenBroadcaster.Core.Services.TransportService Transport { get; }
            public OpenBroadcaster.Core.Services.RadioService RadioService { get; }
            public OpenBroadcaster.Core.Services.LibraryService Library { get; }
            public OpenBroadcaster.Core.Services.CartWallService CartWall { get; }
            public OpenBroadcaster.Core.Overlay.OverlayService OverlayService { get; }

            public ApplicationServices(
                OpenBroadcaster.Core.Messaging.EventBus eventBus,
                OpenBroadcaster.Core.Services.QueueService queue,
                OpenBroadcaster.Core.Services.AudioService audio,
                OpenBroadcaster.Core.Services.TransportService transport,
                OpenBroadcaster.Core.Services.RadioService radioService,
                OpenBroadcaster.Core.Services.LibraryService library,
                OpenBroadcaster.Core.Services.CartWallService cartWall,
                OpenBroadcaster.Core.Overlay.OverlayService overlayService)
            {
                EventBus = eventBus;
                Queue = queue;
                Audio = audio;
                Transport = transport;
                RadioService = radioService;
                Library = library;
                CartWall = cartWall;
                OverlayService = overlayService;
            }
        }
    }
}
