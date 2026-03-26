using System.Text.Json;

namespace Shart.Data.Settings;

/// <summary>
/// Application settings stored as a local JSON file.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Default save directory for incoming files.</summary>
    public string DefaultSavePath { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "Shart");

    /// <summary>Number of parallel TCP data streams (4-8 recommended).</summary>
    public int ParallelStreamCount { get; set; } = 4;

    /// <summary>Control channel port number.</summary>
    public int ControlPort { get; set; } = 9876;

    /// <summary>Data channel starting port.</summary>
    public int DataPortBase { get; set; } = 9877;

    /// <summary>TCP socket buffer size in KB.</summary>
    public int SocketBufferSizeKB { get; set; } = 256;

    /// <summary>Read buffer size in KB (for stream orchestrator).</summary>
    public int ReadBufferSizeKB { get; set; } = 1024;

    /// <summary>Maximum concurrent transfers.</summary>
    public int MaxConcurrentTransfers { get; set; } = 3;

    /// <summary>Enable automatic checksum verification.</summary>
    public bool AutoVerifyChecksum { get; set; } = true;

    /// <summary>UI theme: "Dark" or "Light".</summary>
    public string Theme { get; set; } = "Dark";

    /// <summary>Minimize to system tray on close.</summary>
    public bool MinimizeToTray { get; set; } = true;

    /// <summary>Auto-accept transfers from trusted devices.</summary>
    public bool AutoAcceptTrustedDevices { get; set; } = false;

    /// <summary>Show desktop notification on transfer complete.</summary>
    public bool ShowNotifications { get; set; } = true;
}

/// <summary>
/// Manages loading/saving AppSettings to a JSON file.
/// </summary>
public sealed class AppSettingsManager
{
    private static readonly string SettingsDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Shart");

    private static readonly string SettingsPath = Path.Combine(SettingsDir, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private AppSettings _settings = new();

    /// <summary>Current settings.</summary>
    public AppSettings Settings => _settings;

    /// <summary>
    /// Load settings from disk, or create defaults if not found.
    /// </summary>
    public async Task LoadAsync()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = await File.ReadAllTextAsync(SettingsPath);
                _settings = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions) ?? new AppSettings();
            }
            else
            {
                _settings = new AppSettings();
                await SaveAsync(); // Create default settings file
            }
        }
        catch
        {
            _settings = new AppSettings();
        }

        // Ensure save directory exists
        Directory.CreateDirectory(_settings.DefaultSavePath);
    }

    /// <summary>
    /// Save current settings to disk.
    /// </summary>
    public async Task SaveAsync()
    {
        Directory.CreateDirectory(SettingsDir);
        var json = JsonSerializer.Serialize(_settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json);
    }

    /// <summary>
    /// Update a setting and persist to disk.
    /// </summary>
    public async Task UpdateAsync(Action<AppSettings> configure)
    {
        configure(_settings);
        await SaveAsync();
    }
}
