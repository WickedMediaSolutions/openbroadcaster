using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Linq;
using Avalonia.Markup.Xaml;
using OpenBroadcaster.Avalonia.ViewModels;
using OpenBroadcaster.Avalonia.Views;

namespace OpenBroadcaster.Avalonia
{
    public partial class App : Application
    {
        public override void Initialize()
        {
            AvaloniaXamlLoader.Load(this);
        }

        public override void OnFrameworkInitializationCompleted()
        {
            // Write all diagnostics to a file we can read later
            string logFile = "crash.log";
            string crashDir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenBroadcaster", "logs");
            try { System.IO.Directory.CreateDirectory(crashDir); logFile = System.IO.Path.Combine(crashDir, "crash.log"); } catch { }

            void LogToFile(string msg)
            {
                try { System.IO.File.AppendAllText(logFile, msg + "\n"); } catch { }
                try { System.Console.WriteLine(msg); } catch { }
            }

            try
            {
                LogToFile($"[{DateTime.UtcNow:o}] === APP START ===");
                System.Diagnostics.Trace.Listeners.Add(new System.Diagnostics.TextWriterTraceListener("avalonia-debug.log"));
                System.Diagnostics.Trace.AutoFlush = true;

                AppDomain.CurrentDomain.FirstChanceException += (s, e) =>
                {
                    try
                    {
                        var ex = e.Exception;
                        LogToFile($"[FirstChance] {ex.GetType()}: {ex.Message}");
                    }
                    catch { }
                };

                TaskScheduler.UnobservedTaskException += (s, e) =>
                {
                    try
                    {
                        var ex = e.Exception;
                        LogToFile($"[UnobservedTask] {ex.GetType()}: {ex.Message}");
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
                    LogToFile(fullMsg);
                    LogToFile("\n=== ERROR LOG LOCATION ===");
                    LogToFile($"File: {logFile}");
                    LogToFile("\nOpen this file to see the full error details.");
                }
                catch { }
            };

            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                try
                {
                    LogToFile("[INIT] Creating EventBus...");
                    var eventBus = new OpenBroadcaster.Core.Messaging.EventBus();
                    
                    LogToFile("[INIT] Creating QueueService...");
                    var queue = new OpenBroadcaster.Core.Services.QueueService();
                    
                    LogToFile("[INIT] Creating AudioService...");
                    var audio = new OpenBroadcaster.Core.Services.AudioService();
                    
                    LogToFile("[INIT] Creating TransportService...");
                    var transport = new OpenBroadcaster.Core.Services.TransportService(eventBus, queue, audio);
                    
                    LogToFile("[INIT] Creating RadioService...");
                    var radioService = new OpenBroadcaster.Core.Services.RadioService(transport, eventBus);
                    
                    LogToFile("[INIT] Creating LibraryService...");
                    var library = new OpenBroadcaster.Core.Services.LibraryService();
                    
                    LogToFile("[INIT] Creating CartWallService...");
                    var cartWall = new OpenBroadcaster.Core.Services.CartWallService(audio, null, 16);
                    
                    LogToFile("[INIT] Creating OverlayService...");
                    var overlayService = new OpenBroadcaster.Core.Overlay.OverlayService(queue, eventBus);
                    overlayService.SetLibraryService(library);

                    LogToFile("[INIT] Creating MainWindow...");
                    var mainWindow = new MainWindow();
                    mainWindow.Show();
                    mainWindow.Activate();
                    try { mainWindow.WindowState = WindowState.Maximized; } catch { }

                    LogToFile("[INIT] Setting up file picker...");
                    System.Func<int, System.Threading.Tasks.Task<string?>> filePicker = async (padId) =>
                    {
                        try
                        {
                            var dialog = new OpenFileDialog
                            {
                                Title = "Select audio file for cart pad",
                                AllowMultiple = false
                            };
                            dialog.Filters?.Add(new FileDialogFilter { Name = "Audio Files", Extensions = new List<string> { "mp3", "wav", "flac", "ogg" } });
                            dialog.Filters?.Add(new FileDialogFilter { Name = "All Files", Extensions = new List<string> { "*" } });
                            
                            var results = await dialog.ShowAsync(mainWindow);
                            if (results != null && results.Length > 0 && !string.IsNullOrWhiteSpace(results[0]))
                            {
                                return results[0];
                            }
                            return null;
                        }
                        catch
                        {
                            return null;
                        }
                    };

                    LogToFile("[INIT] Loading app settings...");
                    var settingsStore = new OpenBroadcaster.Core.Services.AppSettingsStore();
                    var appSettings = settingsStore.Load();
                    
                    appSettings.Overlay ??= new OpenBroadcaster.Core.Models.OverlaySettings();
                    if (appSettings.Overlay.Port == 0)
                    {
                        appSettings.Overlay.Port = 9750;
                    }
                    
                    LogToFile("[INIT] Configuring overlay service...");
                    overlayService.UpdateSettings(appSettings.Overlay);

                    LogToFile("[INIT] Creating MainWindowViewModel...");
                    var vm = new MainWindowViewModel(radioService, transport, audio, eventBus, queue, library, cartWall, overlayService, appSettings, filePicker);
                    
                    LogToFile("[INIT] Registering main window...");
                    desktop.MainWindow = mainWindow;
                    mainWindow.DataContext = vm;
                    
                    LogToFile("[INIT] Application startup complete!");
                }
                catch (Exception ex)
                {
                    var fullMsg = $"[INIT ERROR] {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}";
                    LogToFile(fullMsg);
                    LogToFile("\n=== ERROR DETAILS SAVED TO LOG FILE ===");
                    LogToFile($"Location: {logFile}");
                    throw;
                }
            }

            base.OnFrameworkInitializationCompleted();        }
    }
}