using Microsoft.Extensions.Logging;
using Shart.Core.Models;
using Shart.Core.Services;

namespace Shart.Core.Session;

/// <summary>
/// Manages the lifecycle of a connection with a remote device.
/// Handles handshake, PIN authorization, and connection heartbeat.
/// </summary>
public sealed class SessionController : ISessionService, IDisposable
{
    private readonly ILogger<SessionController> _logger;
    private string? _currentPin;
    private CancellationTokenSource? _heartbeatCts;
    private Task? _heartbeatTask;
    private DeviceProfile? _connectedDevice;

    public bool IsConnected { get; private set; }
    public DeviceProfile? ConnectedDevice => _connectedDevice;

    public event EventHandler<ConnectionStateChangedEventArgs>? ConnectionStateChanged;
    public event EventHandler? ConnectionLost;

    /// <summary>Heartbeat interval in seconds.</summary>
    public int HeartbeatIntervalSeconds { get; set; } = 5;

    /// <summary>Maximum missed heartbeats before declaring connection lost.</summary>
    public int MaxMissedHeartbeats { get; set; } = 3;

    public SessionController(ILogger<SessionController> logger)
    {
        _logger = logger;
    }

    public async Task<bool> ConnectAsync(DeviceProfile device, string? pin = null, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Connecting to device: {Name} ({Id})", device.DeviceName, device.DeviceId);

            // Verify PIN if provided
            if (pin != null && !VerifyPin(pin))
            {
                _logger.LogWarning("PIN verification failed for device: {Name}", device.DeviceName);
                return false;
            }

            // Establish connection (will be wired to WiFiDirectWrapper)
            _connectedDevice = device;
            IsConnected = true;

            // Start heartbeat monitoring
            StartHeartbeat();

            ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
            {
                IsConnected = true,
                Device = device
            });

            _logger.LogInformation("Connected to device: {Name}", device.DeviceName);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to connect to device: {Name}", device.DeviceName);
            return false;
        }
    }

    public Task DisconnectAsync()
    {
        StopHeartbeat();

        var device = _connectedDevice;
        _connectedDevice = null;
        IsConnected = false;

        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
        {
            IsConnected = false,
            Device = device,
            DisconnectReason = "User initiated disconnect"
        });

        _logger.LogInformation("Disconnected from device: {Name}", device?.DeviceName ?? "Unknown");
        return Task.CompletedTask;
    }

    public string GenerateConnectionPin()
    {
        _currentPin = Random.Shared.Next(100000, 999999).ToString();
        _logger.LogInformation("Generated connection PIN: {Pin}", _currentPin);
        return _currentPin;
    }

    public bool VerifyPin(string pin)
    {
        return _currentPin != null && _currentPin == pin;
    }

    /// <summary>
    /// Get connection info for QR code generation.
    /// </summary>
    public ConnectionInfo GetConnectionInfo(string machineName, string ipAddress)
    {
        var pin = GenerateConnectionPin();
        return new ConnectionInfo
        {
            MachineName = machineName,
            IpAddress = ipAddress,
            Port = 9876,
            Pin = pin
        };
    }

    private void StartHeartbeat()
    {
        _heartbeatCts = new CancellationTokenSource();
        _heartbeatTask = RunHeartbeatAsync(_heartbeatCts.Token);
    }

    private void StopHeartbeat()
    {
        _heartbeatCts?.Cancel();
        _heartbeatCts?.Dispose();
        _heartbeatCts = null;
    }

    private async Task RunHeartbeatAsync(CancellationToken ct)
    {
        int missedBeats = 0;

        while (!ct.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(HeartbeatIntervalSeconds), ct);

                // TODO: Send heartbeat ping over the control channel
                // For now, simulate — will be wired to the actual connection
                bool alive = IsConnected && _connectedDevice != null;

                if (!alive)
                {
                    missedBeats++;
                    _logger.LogWarning("Missed heartbeat #{Count} for device: {Name}",
                        missedBeats, _connectedDevice?.DeviceName);

                    if (missedBeats >= MaxMissedHeartbeats)
                    {
                        _logger.LogError("Connection lost: {Name} (missed {Count} heartbeats)",
                            _connectedDevice?.DeviceName, missedBeats);

                        IsConnected = false;
                        ConnectionLost?.Invoke(this, EventArgs.Empty);
                        ConnectionStateChanged?.Invoke(this, new ConnectionStateChangedEventArgs
                        {
                            IsConnected = false,
                            Device = _connectedDevice,
                            DisconnectReason = $"Lost connection (missed {missedBeats} heartbeats)"
                        });
                        break;
                    }
                }
                else
                {
                    missedBeats = 0;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    public void Dispose()
    {
        StopHeartbeat();
    }
}

/// <summary>
/// Connection information for QR code generation.
/// </summary>
public sealed class ConnectionInfo
{
    public required string MachineName { get; init; }
    public required string IpAddress { get; init; }
    public required int Port { get; init; }
    public required string Pin { get; init; }

    /// <summary>Serialized for QR code content.</summary>
    public string ToQrString() => $"shart://{MachineName}@{IpAddress}:{Port}?pin={Pin}";
}
