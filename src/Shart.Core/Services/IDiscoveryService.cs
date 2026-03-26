using Shart.Core.Models;

namespace Shart.Core.Services;

/// <summary>
/// Service interface for discovering nearby devices via Wi-Fi Direct.
/// </summary>
public interface IDiscoveryService
{
    /// <summary>Start scanning for nearby devices.</summary>
    Task StartDiscoveryAsync(CancellationToken ct = default);

    /// <summary>Stop scanning for nearby devices.</summary>
    Task StopDiscoveryAsync();

    /// <summary>Whether discovery is currently active.</summary>
    bool IsDiscovering { get; }

    /// <summary>Currently discovered devices.</summary>
    IReadOnlyList<DeviceProfile> DiscoveredDevices { get; }

    /// <summary>Fired when a new device is found.</summary>
    event EventHandler<DeviceDiscoveredEventArgs>? DeviceDiscovered;

    /// <summary>Fired when a device is no longer visible.</summary>
    event EventHandler<DeviceLostEventArgs>? DeviceLost;

    /// <summary>Fired when discovery state changes.</summary>
    event EventHandler<bool>? DiscoveryStateChanged;
}

public sealed class DeviceDiscoveredEventArgs : EventArgs
{
    public required DeviceProfile Device { get; init; }
}

public sealed class DeviceLostEventArgs : EventArgs
{
    public required string DeviceId { get; init; }
}
