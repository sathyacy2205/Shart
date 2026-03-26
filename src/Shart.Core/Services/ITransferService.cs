using Shart.Core.Models;
using Shart.Core.Transfer;

namespace Shart.Core.Services;

/// <summary>
/// Service interface for managing file transfers.
/// </summary>
public interface ITransferService
{
    /// <summary>Enqueue files for sending to a remote device.</summary>
    Task<IReadOnlyList<TransferJob>> SendFilesAsync(DeviceProfile device, IEnumerable<string> filePaths, CancellationToken ct = default);

    /// <summary>Accept an incoming transfer request from a remote device.</summary>
    Task<TransferJob> AcceptTransferAsync(FileMetadata metadata, DeviceProfile sender, string savePath, CancellationToken ct = default);

    /// <summary>Pause a running transfer.</summary>
    Task PauseTransferAsync(Guid jobId);

    /// <summary>Resume a paused transfer.</summary>
    Task ResumeTransferAsync(Guid jobId);

    /// <summary>Cancel a transfer (running or queued).</summary>
    Task CancelTransferAsync(Guid jobId);

    /// <summary>Get all active and queued transfer jobs.</summary>
    IReadOnlyList<TransferJob> GetActiveTransfers();

    /// <summary>Fired when a new incoming transfer request arrives.</summary>
    event EventHandler<IncomingTransferEventArgs>? IncomingTransferRequested;

    /// <summary>Fired when any transfer's progress changes.</summary>
    event EventHandler<TransferProgressEventArgs>? TransferProgressChanged;

    /// <summary>Fired when a transfer completes (success or failure).</summary>
    event EventHandler<TransferCompletedEventArgs>? TransferCompleted;
}

public sealed class IncomingTransferEventArgs : EventArgs
{
    public required FileMetadata FileMetadata { get; init; }
    public required DeviceProfile Sender { get; init; }
}

public sealed class TransferProgressEventArgs : EventArgs
{
    public required Guid JobId { get; init; }
    public required long BytesTransferred { get; init; }
    public required double SpeedBytesPerSecond { get; init; }
    public required double ProgressPercentage { get; init; }
}

public sealed class TransferCompletedEventArgs : EventArgs
{
    public required Guid JobId { get; init; }
    public required TransferStatus FinalStatus { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ChecksumResult { get; init; }
}
