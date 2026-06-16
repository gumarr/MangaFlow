using System;
using System.IO;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.UI.Xaml;
using MangaFlow.Application;
using MangaFlow.Persistence;
using MangaFlow.OCR;
using MangaFlow.AI;
using MangaFlow.Infrastructure;
using MangaFlow.App.ViewModels;

namespace MangaFlow.App;

/// <summary>
/// Provides application-specific behavior to supplement the default Application class.
/// </summary>
public partial class App : Microsoft.UI.Xaml.Application
{
    private Window? _window;
    
    public IServiceProvider ServiceProvider { get; private set; }

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

        // 4. Add ViewModels
        services.AddTransient<MainViewModel>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ProjectsViewModel>();
        services.AddTransient<GlossaryViewModel>();
        services.AddTransient<HistoryViewModel>();
        services.AddTransient<SettingsViewModel>();
    }

    /// <summary>
    /// Invoked when the application is launched.
    /// </summary>
    /// <param name="args">Details about the launch request and process.</param>
    protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
    }
}
