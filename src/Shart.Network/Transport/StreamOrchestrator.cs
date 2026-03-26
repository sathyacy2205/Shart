using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace Shart.Network.Transport;

/// <summary>
/// Represents a chunk of data flowing through the transfer pipeline.
/// </summary>
public sealed class DataChunk
{
    /// <summary>File this chunk belongs to.</summary>
    public Guid FileId { get; init; }

    /// <summary>Sequential index of this chunk.</summary>
    public int ChunkIndex { get; init; }

    /// <summary>Byte offset within the file.</summary>
    public long FileOffset { get; init; }

    /// <summary>The raw data bytes.</summary>
    public required Memory<byte> Data { get; init; }

    /// <summary>Actual length of valid data in the buffer.</summary>
    public int Length { get; init; }

    /// <summary>Which data stream this chunk arrived from / should be sent on.</summary>
    public int StreamIndex { get; init; }
}

/// <summary>
/// High-performance producer/consumer orchestrator using System.Threading.Channels.
/// Safely moves data chunks from network streams to the storage layer
/// (or from storage to network) without race conditions.
/// 
/// Uses bounded channels to apply backpressure when the consumer (disk) 
/// can't keep up with the producer (network).
/// </summary>
public sealed class StreamOrchestrator : IAsyncDisposable
{
    private readonly ILogger<StreamOrchestrator> _logger;
    private readonly ConnectionMultiplexer _multiplexer;

    /// <summary>Channel capacity (number of chunks buffered). Applies backpressure beyond this.</summary>
    public int ChannelCapacity { get; init; } = 64;

    /// <summary>Size of each read buffer in bytes (1MB).</summary>
    public int ReadBufferSize { get; init; } = 1024 * 1024;

    private Channel<DataChunk>? _receiveChannel;
    private Channel<DataChunk>? _sendChannel;
    private readonly List<Task> _workerTasks = [];
    private CancellationTokenSource? _cts;

    public StreamOrchestrator(ILogger<StreamOrchestrator> logger, ConnectionMultiplexer multiplexer)
    {
        _logger = logger;
        _multiplexer = multiplexer;
    }

    /// <summary>
    /// Start receiving data from all data streams into the receive channel.
    /// The storage layer reads from the returned ChannelReader.
    /// </summary>
    public ChannelReader<DataChunk> StartReceiving(Guid fileId, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _receiveChannel = Channel.CreateBounded<DataChunk>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        var streams = _multiplexer.GetAllDataStreams();
        for (int i = 0; i < streams.Count; i++)
        {
            int streamIndex = i;
            var task = Task.Run(() => ReceiveWorkerAsync(fileId, streamIndex, _cts.Token), _cts.Token);
            _workerTasks.Add(task);
        }

        _logger.LogInformation("Started {N} receive workers for file {FileId}", streams.Count, fileId);

        // Complete the channel when all workers finish
        _ = Task.Run(async () =>
        {
            await Task.WhenAll(_workerTasks);
            _receiveChannel.Writer.TryComplete();
            _logger.LogInformation("All receive workers completed for file {FileId}", fileId);
        });

        return _receiveChannel.Reader;
    }

    /// <summary>
    /// Start sending data chunks from the send channel across all data streams.
    /// The storage layer writes to the returned ChannelWriter.
    /// </summary>
    public ChannelWriter<DataChunk> StartSending(CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        _sendChannel = Channel.CreateBounded<DataChunk>(new BoundedChannelOptions(ChannelCapacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = false,
            SingleWriter = false
        });

        // Round-robin sender across data streams
        var task = Task.Run(() => SendWorkerAsync(_cts.Token), _cts.Token);
        _workerTasks.Add(task);

        _logger.LogInformation("Started send worker with {N} data streams", _multiplexer.GetAllDataStreams().Count);

        return _sendChannel.Writer;
    }

    /// <summary>
    /// Get the total bytes received so far (for progress tracking).
    /// </summary>
    public long TotalBytesReceived => _totalBytesReceived;
    private long _totalBytesReceived;

    /// <summary>
    /// Get the total bytes sent so far (for progress tracking).
    /// </summary>
    public long TotalBytesSent => _totalBytesSent;
    private long _totalBytesSent;

    private async Task ReceiveWorkerAsync(Guid fileId, int streamIndex, CancellationToken ct)
    {
        var buffer = new byte[ReadBufferSize];
        int chunkIndex = 0;

        try
        {
            while (!ct.IsCancellationRequested)
            {
                int bytesRead = await _multiplexer.ReadDataAsync(streamIndex, buffer, ct);
                if (bytesRead == 0) break; // Stream ended

                // Copy data (buffer will be reused)
                var chunkData = new byte[bytesRead];
                buffer.AsSpan(0, bytesRead).CopyTo(chunkData);

                var chunk = new DataChunk
                {
                    FileId = fileId,
                    ChunkIndex = chunkIndex++,
                    FileOffset = Interlocked.Read(ref _totalBytesReceived),
                    Data = chunkData,
                    Length = bytesRead,
                    StreamIndex = streamIndex
                };

                await _receiveChannel!.Writer.WriteAsync(chunk, ct);
                Interlocked.Add(ref _totalBytesReceived, bytesRead);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Receive worker {Index} failed", streamIndex);
        }

        _logger.LogDebug("Receive worker {Index} finished. Total received: {Bytes} bytes",
            streamIndex, Interlocked.Read(ref _totalBytesReceived));
    }

    private async Task SendWorkerAsync(CancellationToken ct)
    {
        var streams = _multiplexer.GetAllDataStreams();
        int streamCount = streams.Count;
        int roundRobin = 0;

        try
        {
            await foreach (var chunk in _sendChannel!.Reader.ReadAllAsync(ct))
            {
                // Round-robin across data streams
                int streamIndex = roundRobin % streamCount;
                roundRobin++;

                await _multiplexer.WriteDataAsync(streamIndex, chunk.Data, ct);
                Interlocked.Add(ref _totalBytesSent, chunk.Length);
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Send worker failed");
        }

        // Flush all streams
        foreach (var stream in streams)
        {
            try { await stream.FlushAsync(ct); } catch { }
        }

        _logger.LogDebug("Send worker finished. Total sent: {Bytes} bytes",
            Interlocked.Read(ref _totalBytesSent));
    }

    /// <summary>Stop all workers gracefully.</summary>
    public async Task StopAsync()
    {
        _cts?.Cancel();
        _sendChannel?.Writer.TryComplete();

        if (_workerTasks.Count > 0)
        {
            await Task.WhenAll(_workerTasks);
        }
    }

    public async ValueTask DisposeAsync()
    {
        await StopAsync();
        _cts?.Dispose();
    }
}
