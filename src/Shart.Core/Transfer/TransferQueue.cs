using System.Collections.Concurrent;
using Shart.Core.Models;

namespace Shart.Core.Transfer;

/// <summary>
/// Thread-safe priority queue for managing transfer jobs.
/// Higher priority items (e.g., small files) are dequeued first.
/// </summary>
public sealed class TransferQueue
{
    private readonly ConcurrentQueue<TransferJob> _highPriority = new();
    private readonly ConcurrentQueue<TransferJob> _normalPriority = new();
    private readonly ConcurrentQueue<TransferJob> _lowPriority = new();
    private readonly SemaphoreSlim _signal = new(0);

    /// <summary>Total number of items across all priority levels.</summary>
    public int Count => _highPriority.Count + _normalPriority.Count + _lowPriority.Count;

    /// <summary>
    /// Enqueue a transfer job. Files under 10MB get high priority,
    /// files over 1GB get low priority, everything else is normal.
    /// </summary>
    public void Enqueue(TransferJob job)
    {
        var queue = job.FileMetadata.FileSizeBytes switch
        {
            < 10 * 1024 * 1024 => _highPriority,        // < 10MB
            > 1024L * 1024 * 1024 => _lowPriority,      // > 1GB
            _ => _normalPriority
        };

        queue.Enqueue(job);
        _signal.Release();
    }

    /// <summary>
    /// Dequeue the next highest-priority job. Blocks until one is available.
    /// </summary>
    public async Task<TransferJob> DequeueAsync(CancellationToken ct = default)
    {
        await _signal.WaitAsync(ct);

        if (_highPriority.TryDequeue(out var highJob)) return highJob;
        if (_normalPriority.TryDequeue(out var normalJob)) return normalJob;
        if (_lowPriority.TryDequeue(out var lowJob)) return lowJob;

        throw new InvalidOperationException("Signal was released but no item found in queues.");
    }

    /// <summary>
    /// Try to dequeue without blocking.
    /// </summary>
    public bool TryDequeue(out TransferJob? job)
    {
        if (_highPriority.TryDequeue(out job)) return true;
        if (_normalPriority.TryDequeue(out job)) return true;
        if (_lowPriority.TryDequeue(out job)) return true;

        job = null;
        return false;
    }

    /// <summary>
    /// Get a snapshot of all queued jobs (all priorities).
    /// </summary>
    public IReadOnlyList<TransferJob> GetSnapshot()
    {
        var list = new List<TransferJob>();
        list.AddRange(_highPriority);
        list.AddRange(_normalPriority);
        list.AddRange(_lowPriority);
        return list;
    }
}
