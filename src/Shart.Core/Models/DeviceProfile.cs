namespace Shart.Core.Models;

/// <summary>
/// Represents a discovered or trusted remote device.
/// </summary>
public sealed class DeviceProfile
{
    /// <summary>Unique identifier for this device.</summary>
    public required string DeviceId { get; init; }

    /// <summary>Human-readable device name (e.g., "Pixel 8 Pro").</summary>
    public required string DeviceName { get; init; }

    /// <summary>IP address assigned during Wi-Fi Direct session.</summary>
    public string? IpAddress { get; set; }

    /// <summary>Port number the device is listening on.</summary>
    public int Port { get; set; } = 9876;

    /// <summary>Whether this device has been previously trusted/authorized.</summary>
    public bool IsTrusted { get; set; }

    /// <summary>Device type (Android, Windows, etc.).</summary>
    public DeviceType Type { get; init; } = DeviceType.Android;

    /// <summary>Signal strength indicator (-100 to 0 dBm).</summary>
    public int SignalStrength { get; set; }

    /// <summary>When this device was first discovered in the current session.</summary>
    public DateTime DiscoveredAtUtc { get; init; } = DateTime.UtcNow;

    /// <summary>When this device was last seen.</summary>
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Type of remote device.
/// </summary>
public enum DeviceType
{
    Android,
    Windows,
    Unknown
}
