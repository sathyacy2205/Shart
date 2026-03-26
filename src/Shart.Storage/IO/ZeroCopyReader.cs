using System.IO.MemoryMappedFiles;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace Shart.Storage.IO;

/// <summary>
/// Zero-copy file reader for sending files to remote devices.
/// Reads data directly from the file into the network socket buffer,
/// minimizing RAM overhead by using memory-mapped files.
/// 
/// Standard read path:  Disk → Kernel Buffer → App Buffer → Network Socket
/// Zero-copy read path: Disk → Kernel Page Cache → Network Socket (via mmap)
/// </summary>
public sealed class ZeroCopyReader : IDisposable
{
    private readonly ILogger<ZeroCopyReader> _logger;
    private MemoryMappedFile? _mmf;
    private readonly string _filePath;
    private readonly long _fileSize;
    private bool _disposed;

    /// <summary>Default read chunk size (1MB).</summary>
    public int ChunkSize { get; init; } = 1024 * 1024;

    public ZeroCopyReader(ILogger<ZeroCopyReader> logger, string filePath)
    {
        _logger = logger;
        _filePath = filePath;

        var fi = new FileInfo(filePath);
        if (!fi.Exists) throw new FileNotFoundException("Source file not found.", filePath);
        _fileSize = fi.Length;
    }

    /// <summary>Total file size in bytes.</summary>
    public long FileSize => _fileSize;

    /// <summary>
    /// Initialize the memory-mapped reader.
    /// </summary>
    public void Initialize()
    {
        _mmf = MemoryMappedFile.CreateFromFile(
            _filePath,
            FileMode.Open,
            mapName: null,
            capacity: 0, // Use actual file size
            access: MemoryMappedFileAccess.Read);

        _logger.LogInformation("Zero-copy reader initialized: {Path} ({Size} bytes)", _filePath, _fileSize);
    }

    /// <summary>
    /// Read a chunk at a specific offset. Returns the actual bytes read.
    /// Uses a MemoryMappedViewStream for sequential access efficiency.
    /// </summary>
    public int ReadChunk(long offset, Span<byte> buffer)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_mmf == null)
            throw new InvalidOperationException("Reader not initialized. Call Initialize() first.");

        long remaining = _fileSize - offset;
        int toRead = (int)Math.Min(buffer.Length, remaining);

        if (toRead <= 0) return 0;

        using var viewStream = _mmf.CreateViewStream(offset, toRead, MemoryMappedFileAccess.Read);
        return viewStream.Read(buffer[..toRead]);
    }

    /// <summary>
    /// Read a chunk asynchronously.
    /// </summary>
    public async Task<int> ReadChunkAsync(long offset, Memory<byte> buffer, CancellationToken ct = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_mmf == null)
            throw new InvalidOperationException("Reader not initialized. Call Initialize() first.");

        long remaining = _fileSize - offset;
        int toRead = (int)Math.Min(buffer.Length, remaining);

        if (toRead <= 0) return 0;

        using var viewStream = _mmf.CreateViewStream(offset, toRead, MemoryMappedFileAccess.Read);
        return await viewStream.ReadAsync(buffer[..toRead], ct);
    }

    /// <summary>
    /// Calculate the number of chunks for the entire file given a chunk size.
    /// </summary>
    public int CalculateChunkCount(int chunkSize)
    {
        return (int)Math.Ceiling((double)_fileSize / chunkSize);
    }

    /// <summary>
    /// Generate chunk offsets and sizes for parallel stream distribution.
    /// </summary>
    public IReadOnlyList<(long Offset, int Size)> GenerateChunkLayout(int chunkSize)
    {
        var chunks = new List<(long, int)>();
        long offset = 0;

        while (offset < _fileSize)
        {
            int size = (int)Math.Min(chunkSize, _fileSize - offset);
            chunks.Add((offset, size));
            offset += size;
        }

        return chunks;
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;

        _mmf?.Dispose();
        _logger.LogInformation("ZeroCopyReader disposed: {Path}", _filePath);
    }
}
