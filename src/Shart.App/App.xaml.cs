using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.EntityFrameworkCore;
using Serilog;
using Shart.Core.Services;
using Shart.Core.Session;
using Shart.Core.Transfer;
using Shart.Data.Database;
using Shart.Data.Settings;
using Shart.Network.Transport;
using Shart.Storage.IO;
using Shart.Storage.Integrity;

namespace Shart.App;

public partial class App : Application
{
    private IHost? _host;

    public static IServiceProvider Services { get; private set; } = null!;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Configure Serilog
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Information()
            .WriteTo.File(
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "Shart", "logs", "shart-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        // Build host with DI
        _host = Host.CreateDefaultBuilder()
            .UseSerilog()
            .ConfigureServices((context, services) =>
            {
                // Settings
                services.AddSingleton<AppSettingsManager>();

                // Database
                services.AddDbContext<ShartDbContext>(options =>
                {
                    var dbPath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                        "Shart", "shart.db");
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(dbPath)!);
                    options.UseSqlite($"Data Source={dbPath}");
                });

                // Core services
                services.AddSingleton<TransferManager>();
                services.AddSingleton<ITransferService>(sp => sp.GetRequiredService<TransferManager>());
                services.AddSingleton<SessionController>();
                services.AddSingleton<ISessionService>(sp => sp.GetRequiredService<SessionController>());

                // Network
                services.AddTransient<ConnectionMultiplexer>();
                services.AddTransient<StreamOrchestrator>();

                // Storage
                services.AddTransient<IntegrityChecker>();

                // ViewModels
                services.AddTransient<ViewModels.MainViewModel>();
                services.AddTransient<ViewModels.RadarViewModel>();
                services.AddTransient<ViewModels.TransferQueueViewModel>();
                services.AddTransient<ViewModels.QrCodeViewModel>();

                // Main Window
                services.AddTransient<MainWindow>();
            })
            .Build();

        Services = _host.Services;

        // Initialize settings
        var settingsManager = Services.GetRequiredService<AppSettingsManager>();
        await settingsManager.LoadAsync();

        // Initialize database
        using (var scope = Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ShartDbContext>();
            await ShartDbContext.InitializeAsync(db);
        }

        // Start transfer manager
        var transferManager = Services.GetRequiredService<TransferManager>();
        transferManager.Start();

        Log.Information("Shart application started successfully.");
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        var transferManager = Services.GetRequiredService<TransferManager>();
        await transferManager.StopAsync();

        Log.Information("Shart application shutting down.");
        await Log.CloseAndFlushAsync();

        _host?.Dispose();
        base.OnExit(e);
    }
}
