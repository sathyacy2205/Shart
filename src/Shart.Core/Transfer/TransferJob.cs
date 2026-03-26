using CommunityToolkit.Mvvm.ComponentModel;
using Shart.Core.Models;

namespace Shart.Core.Transfer;

/// <summary>
/// Represents a single file transfer job with real-time progress tracking.
/// Observable properties for WPF data binding.
/// </summary>
public partial class TransferJob : ObservableObject
{
    /// <summary>Unique ID for this transfer job.</summary>
    public Guid JobId { get; } = Guid.NewGuid();

    /// <summary>Metadata of the file being transferred.</summary>
    public required FileMetadata FileMetadata { get; init; }

    /// <summary>Direction of transfer (incoming/outgoing).</summary>
    public required TransferDirection Direction { get; init; }

    /// <summary>Remote device involved in this transfer.</summary>
    public required DeviceProfile RemoteDevice { get; init; }

    /// <summary>When this job was created.</summary>
    public DateTime CreatedAtUtc { get; } = DateTime.UtcNow;

    /// <summary>When this job started transferring.</summary>
    public DateTime? StartedAtUtc { get; set; }

    /// <summary>When this job completed.</summary>
    public DateTime? CompletedAtUtc { get; set; }

    [ObservableProperty]
    private TransferStatus _status = TransferStatus.Pending;

    [ObservableProperty]
    private long _bytesTransferred;

    [ObservableProperty]
    private double _progressPercentage;

    [ObservableProperty]
    private double _speedBytesPerSecond;

    [ObservableProperty]
    private TimeSpan _estimatedTimeRemaining;

    [ObservableProperty]
    private string? _errorMessage;

    /// <summary>Number of parallel streams used for this transfer.</summary>
    public int ParallelStreamCount { get; set; } = 4;

    /// <summary>Cancellation token source for this job.</summary>
    public CancellationTokenSource CancellationTokenSource { get; } = new();

    /// <summary>Human-readable speed display.</summary>
    public string FormattedSpeed
    {
        get
        {
            if (SpeedBytesPerSecond < 1024) return $"{SpeedBytesPerSecond:F0} B/s";
            if (SpeedBytesPerSecond < 1024 * 1024) return $"{SpeedBytesPerSecond / 1024:F1} KB/s";
            if (SpeedBytesPerSecond < 1024 * 1024 * 1024) return $"{SpeedBytesPerSecond / (1024 * 1024):F1} MB/s";
            return $"{SpeedBytesPerSecond / (1024.0 * 1024 * 1024):F2} GB/s";
        }
    }

    /// <summary>
    /// Updates progress metrics. Call from the transfer engine.
    /// </summary>
    public void UpdateProgress(long newBytesTransferred, double speedBps)
    {
        BytesTransferred = newBytesTransferred;
        SpeedBytesPerSecond = speedBps;

        if (FileMetadata.FileSizeBytes > 0)
        {
            ProgressPercentage = (double)newBytesTransferred / FileMetadata.FileSizeBytes * 100.0;

            if (speedBps > 0)
            {
                var remainingBytes = FileMetadata.FileSizeBytes - newBytesTransferred;
                EstimatedTimeRemaining = TimeSpan.FromSeconds(remainingBytes / speedBps);
            }
        }

        OnPropertyChanged(nameof(FormattedSpeed));
    }
}
