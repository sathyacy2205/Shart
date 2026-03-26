using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Shart.Core.Models;
using Shart.Core.Services;

namespace Shart.Core.Transfer;

/// <summary>
/// Singleton master controller for all file transfers.
/// Maintains the queue, tracks progress, handles pause/cancel.
/// </summary>
public sealed class TransferManager : ITransferService, IDisposable
{
    private readonly ILogger<TransferManager> _logger;
    private readonly TransferQueue _queue = new();
    private readonly ConcurrentDictionary<Guid, TransferJob> _activeJobs = new();
    private readonly ConcurrentDictionary<Guid, TransferJob> _allJobs = new();
    private readonly SemaphoreSlim _maxConcurrent;
    private CancellationTokenSource _managerCts = new();
    private Task? _processingTask;

    /// <summary>Maximum number of concurrent transfers.</summary>
    public int MaxConcurrentTransfers { get; set; } = 3;

    public event EventHandler<IncomingTransferEventArgs>? IncomingTransferRequested;
    public event EventHandler<TransferProgressEventArgs>? TransferProgressChanged;
    public event EventHandler<TransferCompletedEventArgs>? TransferCompleted;

    public TransferManager(ILogger<TransferManager> logger)
    {
        _logger = logger;
        _maxConcurrent = new SemaphoreSlim(MaxConcurrentTransfers, MaxConcurrentTransfers);
    }

    /// <summary>Start the transfer processing loop.</summary>
    public void Start()
    {
        _managerCts = new CancellationTokenSource();
        _processingTask = ProcessQueueAsync(_managerCts.Token);
        _logger.LogInformation("TransferManager started. Max concurrent: {Max}", MaxConcurrentTransfers);
    }

    /// <summary>Stop the transfer processing loop.</summary>
    public async Task StopAsync()
    {
        _managerCts.Cancel();
        if (_processingTask != null)
        {
            await _processingTask;
        }
        _logger.LogInformation("TransferManager stopped.");
    }

    public Task<IReadOnlyList<TransferJob>> SendFilesAsync(DeviceProfile device, IEnumerable<string> filePaths, CancellationToken ct = default)
    {
        var jobs = new List<TransferJob>();

        foreach (var path in filePaths)
        {
            var fileInfo = new FileInfo(path);
            if (!fileInfo.Exists)
            {
                _logger.LogWarning("File not found: {Path}", path);
                continue;
            }

            var job = new TransferJob
            {
                FileMetadata = new FileMetadata
                {
                    FileName = fileInfo.Name,
                    SourcePath = fileInfo.FullName,
                    FileSizeBytes = fileInfo.Length,
                    LastModifiedUtc = fileInfo.LastWriteTimeUtc
                },
                Direction = TransferDirection.Outgoing,
                RemoteDevice = device
            };

            _allJobs[job.JobId] = job;
            _queue.Enqueue(job);
            jobs.Add(job);

            _logger.LogInformation("Queued outgoing transfer: {File} ({Size}) to {Device}",
                job.FileMetadata.FileName, job.FileMetadata.FormattedSize, device.DeviceName);
        }

        return Task.FromResult<IReadOnlyList<TransferJob>>(jobs);
    }

    public Task<TransferJob> AcceptTransferAsync(FileMetadata metadata, DeviceProfile sender, string savePath, CancellationToken ct = default)
    {
        metadata.DestinationPath = Path.Combine(savePath, metadata.FileName);
        var job = new TransferJob
        {
            FileMetadata = metadata,
            Direction = TransferDirection.Incoming,
            RemoteDevice = sender
        };

        _allJobs[job.JobId] = job;
        _queue.Enqueue(job);

        _logger.LogInformation("Accepted incoming transfer: {File} ({Size}) from {Device}",
            metadata.FileName, metadata.FormattedSize, sender.DeviceName);

        return Task.FromResult(job);
    }

    public Task PauseTransferAsync(Guid jobId)
    {
        if (_allJobs.TryGetValue(jobId, out var job) && job.Status == TransferStatus.InProgress)
        {
            job.Status = TransferStatus.Paused;
            _logger.LogInformation("Paused transfer: {File}", job.FileMetadata.FileName);
        }
        return Task.CompletedTask;
    }

    public Task ResumeTransferAsync(Guid jobId)
    {
        if (_allJobs.TryGetValue(jobId, out var job) && job.Status == TransferStatus.Paused)
        {
            job.Status = TransferStatus.InProgress;
            _logger.LogInformation("Resumed transfer: {File}", job.FileMetadata.FileName);
        }
        return Task.CompletedTask;
    }

    public Task CancelTransferAsync(Guid jobId)
    {
        if (_allJobs.TryGetValue(jobId, out var job))
        {
            job.CancellationTokenSource.Cancel();
            job.Status = TransferStatus.Cancelled;
            _logger.LogInformation("Cancelled transfer: {File}", job.FileMetadata.FileName);
        }
        return Task.CompletedTask;
    }

    public IReadOnlyList<TransferJob> GetActiveTransfers()
    {
        return _allJobs.Values
            .Where(j => j.Status is TransferStatus.InProgress or TransferStatus.Pending or TransferStatus.Paused)
            .OrderBy(j => j.CreatedAtUtc)
            .ToList();
    }

    /// <summary>
    /// Raise an incoming transfer request event (called by the network layer).
    /// </summary>
    public void RaiseIncomingTransferRequest(FileMetadata metadata, DeviceProfile sender)
    {
        IncomingTransferRequested?.Invoke(this, new IncomingTransferEventArgs
        {
            FileMetadata = metadata,
            Sender = sender
        });
    }

    /// <summary>
    /// Report progress from the network/storage layer.
    /// </summary>
    public void ReportProgress(Guid jobId, long bytesTransferred, double speedBps)
    {
        if (_allJobs.TryGetValue(jobId, out var job))
        {
            job.UpdateProgress(bytesTransferred, speedBps);

            TransferProgressChanged?.Invoke(this, new TransferProgressEventArgs
            {
                JobId = jobId,
                BytesTransferred = bytesTransferred,
                SpeedBytesPerSecond = speedBps,
                ProgressPercentage = job.ProgressPercentage
            });
        }
    }

    /// <summary>
    /// Mark a transfer as completed (called by the network/storage layer).
    /// </summary>
    public void CompleteTransfer(Guid jobId, TransferStatus finalStatus, string? error = null, string? checksum = null)
    {
        if (_allJobs.TryGetValue(jobId, out var job))
        {
            job.Status = finalStatus;
            job.CompletedAtUtc = DateTime.UtcNow;
            job.ErrorMessage = error;
            _activeJobs.TryRemove(jobId, out _);

            TransferCompleted?.Invoke(this, new TransferCompletedEventArgs
            {
                JobId = jobId,
                FinalStatus = finalStatus,
                ErrorMessage = error,
                ChecksumResult = checksum
            });

            _logger.LogInformation("Transfer {Status}: {File} — {Checksum}",
                finalStatus, job.FileMetadata.FileName, checksum ?? "N/A");
        }
    }

    private async Task ProcessQueueAsync(CancellationToken ct)
    {
        _logger.LogInformation("Queue processor started.");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var job = await _queue.DequeueAsync(ct);
                await _maxConcurrent.WaitAsync(ct);

                _activeJobs[job.JobId] = job;
                job.Status = TransferStatus.InProgress;
                job.StartedAtUtc = DateTime.UtcNow;

                // Fire-and-forget the actual transfer execution
                _ = Task.Run(async () =>
                {
                    try
                    {
                        // Placeholder: The actual transfer is driven by the Network engine
                        // which calls ReportProgress() and CompleteTransfer()
                        _logger.LogInformation("Started transfer: {File} ({Direction})",
                            job.FileMetadata.FileName, job.Direction);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Transfer failed: {File}", job.FileMetadata.FileName);
                        CompleteTransfer(job.JobId, TransferStatus.Failed, ex.Message);
                    }
                    finally
                    {
                        _maxConcurrent.Release();
                    }
                }, ct);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        _logger.LogInformation("Queue processor stopped.");
    }

    public void Dispose()
    {
        _managerCts.Cancel();
        _managerCts.Dispose();
        _maxConcurrent.Dispose();

        foreach (var job in _allJobs.Values)
        {
            job.CancellationTokenSource.Dispose();
        }
    }
}
