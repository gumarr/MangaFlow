using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using MangaFlow.Application;
using MangaFlow.Application.Interfaces;
using MangaFlow.Persistence;
using MangaFlow.OCR;
using MangaFlow.AI;
using MangaFlow.Infrastructure;
using MangaFlow.App.ViewModels;
using MangaFlow.App.Services;

namespace MangaFlow.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    private bool _isExiting = false;
    private SystemTrayService? _systemTrayService;

    public static App CurrentApp => (App)Microsoft.UI.Xaml.Application.Current;

    public MainWindow? MainWindowInstance => _window as MainWindow;
    
    public IServiceProvider ServiceProvider { get; private set; }

    public static void LogToFile(string message)
    {
        try
        {
            var logPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MangaFlow", "activation.log");
            var dir = Path.GetDirectoryName(logPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}");
        }
        catch
        {
            // Ignore logging failures
        }
    }

    /// <summary>
    /// Initializes the singleton application object.  This is the first line of authored code
    /// executed, and as such is the logical equivalent of main() or WinMain().
    /// </summary>
    public App()
    {
        InitializeComponent();

        var services = new ServiceCollection();
        ConfigureServices(services);
        ServiceProvider = services.BuildServiceProvider();

        // Initialize SQLite Database schema
        DbInitializer.Initialize(ServiceProvider);
    }

    private void ConfigureServices(IServiceCollection services)
    {
        // 1. Structured Logging
        services.AddLogging(builder =>
        {
            builder.AddDebug();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        // 2. Setup SQLite Connection String in LocalApplicationData
        var localFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MangaFlow");
        if (!Directory.Exists(localFolder))
        {
            Directory.CreateDirectory(localFolder);
        }
        var dbPath = Path.Combine(localFolder, "mangaflow.db");
        var connectionString = $"Data Source={dbPath}";

        // 3. Add Layers
        services.AddPersistence(connectionString);
        services.AddApplication();
        services.AddOcrServices();
        services.AddAiServices();
        services.AddInfrastructureServices();

        // 4. Add ViewModels and Services
        services.AddSingleton<CaptureWorkflow>();
        services.AddSingleton<SystemTrayService>();
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<GlossaryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    public void HideMainWindow()
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _window?.AppWindow.Hide();
        });
    }

    public void ShowMainWindow()
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _window?.AppWindow.Show();
            _window?.Activate();
        });
    }

    public void ToggleMainWindow()
    {
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            if (_window != null)
            {
                if (_window.AppWindow.IsVisible)
                {
                    _window.AppWindow.Hide();
                }
                else
                {
                    _window.AppWindow.Show();
                    _window.Activate();
                }
            }
        });
    }

    public void ExitApplication()
    {
        _isExiting = true;
        _systemTrayService?.Dispose();
        _window?.DispatcherQueue.TryEnqueue(() =>
        {
            _window?.Close();
            Microsoft.UI.Xaml.Application.Current.Exit();
        });
    }

    private void MainWindow_Closed(object sender, WindowEventArgs args)
    {
        if (!_isExiting)
        {
            args.Handled = true;
            HideMainWindow();
        }
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override async void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Closed += MainWindow_Closed;
        _window.Activate();

        try
        {
            var hwnd = WinRT.Interop.WindowNative.GetWindowHandle(_window);
            
            // Initialize System Tray Service
            _systemTrayService = ServiceProvider.GetRequiredService<SystemTrayService>();
            var workflow = ServiceProvider.GetRequiredService<CaptureWorkflow>();

            _systemTrayService.Initialize(
                hwnd,
                _window.DispatcherQueue,
                showAction: () => ToggleMainWindow(),
                captureAction: () =>
                {
                    _window.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await workflow.StartCaptureWorkflowAsync();
                    });
                },
                exitAction: () => ExitApplication()
            );

            var hotkeyService = ServiceProvider.GetRequiredService<IHotkeyService>() as HotkeyService;
            if (hotkeyService != null)
            {
                hotkeyService.Initialize(hwnd);

                var settingsService = ServiceProvider.GetRequiredService<ISettingsService>();
                var settings = await settingsService.GetSettingsAsync();
                var hotkeyCombo = settings?.GlobalHotkey ?? "Alt + Q";

                hotkeyService.RegisterHotkey(hotkeyCombo, () =>
                {
                    _window.DispatcherQueue.TryEnqueue(async () =>
                    {
                        await workflow.StartCaptureWorkflowAsync();
                    });
                });
            }
        }
        catch (Exception ex)
        {
            var logger = ServiceProvider.GetService<ILogger<App>>();
            logger?.LogError(ex, "Failed to initialize services on launch");
        }
    }
}
