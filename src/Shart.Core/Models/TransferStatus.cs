namespace Shart.Core.Models;

/// <summary>
/// Represents the current status of a file transfer.
/// </summary>
public enum TransferStatus
{
    /// <summary>Transfer is queued and waiting to start.</summary>
    Pending,

    /// <summary>Transfer is actively in progress.</summary>
    InProgress,

    /// <summary>Transfer is temporarily paused by the user.</summary>
    Paused,

    /// <summary>Transfer completed successfully.</summary>
    Completed,

    /// <summary>Transfer failed due to an error.</summary>
    Failed,

    /// <summary>Transfer was cancelled by the user.</summary>
    Cancelled,

    /// <summary>Verifying file integrity (checksum).</summary>
    Verifying
}

/// <summary>
/// Direction of the transfer relative to this device.
/// </summary>
public enum TransferDirection
{
    /// <summary>Receiving a file from the remote device.</summary>
    Incoming,

    /// <summary>Sending a file to the remote device.</summary>
    Outgoing
}
