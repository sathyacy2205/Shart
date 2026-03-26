namespace Shart.Data.Entities;

/// <summary>
/// Database entity for transfer history records.
/// </summary>
public sealed class TransferHistoryEntity
{
    public int Id { get; set; }
    public Guid JobId { get; set; }
    public required string FileName { get; set; }
    public long FileSizeBytes { get; set; }
    public string? MimeType { get; set; }
    public required string Direction { get; set; } // "Incoming" or "Outgoing"
    public required string Status { get; set; }     // "Completed", "Failed", "Cancelled"
    public required string DeviceId { get; set; }
    public required string DeviceName { get; set; }
    public string? SavePath { get; set; }
    public string? Checksum { get; set; }
    public double SpeedBytesPerSecond { get; set; }
    public double DurationSeconds { get; set; }
    public DateTime TransferredAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Database entity for trusted/known devices.
/// </summary>
public sealed class TrustedDeviceEntity
{
    public int Id { get; set; }
    public required string DeviceId { get; set; }
    public required string DeviceName { get; set; }
    public string DeviceType { get; set; } = "Android";
    public bool IsTrusted { get; set; } = true;
    public DateTime FirstSeenUtc { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenUtc { get; set; } = DateTime.UtcNow;
    public int TotalTransfers { get; set; }
    public long TotalBytesTransferred { get; set; }
}

/// <summary>
/// Generic key-value preference storage.
/// </summary>
public sealed class UserPreferenceEntity
{
    public int Id { get; set; }
    public required string Key { get; set; }
    public required string Value { get; set; }
    public DateTime UpdatedAtUtc { get; set; } = DateTime.UtcNow;
}
