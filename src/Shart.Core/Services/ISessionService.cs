using Shart.Core.Models;

namespace Shart.Core.Services;

/// <summary>
/// Service interface for managing the session/connection lifecycle with a remote device.
/// </summary>
public interface ISessionService
{
    /// <summary>Connect to a discovered device.</summary>
    Task<bool> ConnectAsync(DeviceProfile device, string? pin = null, CancellationToken ct = default);

    /// <summary>Disconnect from the currently connected device.</summary>
    Task DisconnectAsync();

    /// <summary>Whether a session is currently active.</summary>
    bool IsConnected { get; }

    /// <summary>The currently connected device, if any.</summary>
    DeviceProfile? ConnectedDevice { get; }

    /// <summary>Generate a connection PIN for QR code pairing.</summary>
    string GenerateConnectionPin();

    /// <summary>Verify a PIN provided by the remote device.</summary>
    bool VerifyPin(string pin);

    /// <summary>Fired when connection state changes.</summary>
    event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;

    /// <summary>Fired when the heartbeat detects the connection is lost.</summary>
    event EventHandler? ConnectionLost;
}

public sealed class ConnectionStateChangedEventArgs : EventArgs
{
    public required bool IsConnected { get; init; }
    public DeviceProfile? Device { get; init; }
    public string? DisconnectReason { get; init; }
}
