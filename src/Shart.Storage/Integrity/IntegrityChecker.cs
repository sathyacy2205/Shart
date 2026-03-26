using Microsoft.Extensions.Logging;
using Standart.Hash.xxHash;

namespace Shart.Storage.Integrity;

/// <summary>
/// Verifies file integrity after transfer using xxHash64 (extremely fast).
/// xxHash64 processes at ~30 GB/s on modern CPUs, making it negligible overhead.
/// </summary>
public sealed class IntegrityChecker
{
    private readonly ILogger<IntegrityChecker> _logger;

    /// <summary>Buffer size for reading during checksum (4MB).</summary>
    public int BufferSize { get; init; } = 4 * 1024 * 1024;

    public IntegrityChecker(ILogger<IntegrityChecker> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Compute xxHash64 checksum for a file.
    /// </summary>
    public async Task<string> ComputeChecksumAsync(string filePath, CancellationToken ct = default)
    {
        _logger.LogInformation("Computing checksum for: {Path}", filePath);
        var sw = System.Diagnostics.Stopwatch.StartNew();

        await using var stream = new FileStream(
            filePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            bufferSize: BufferSize,
            useAsync: true);

        var hash = await xxHash64.ComputeHashAsync(stream, BufferSize, seed: 0, cancellationToken: ct);
        var checksum = hash.ToString("X16");

        sw.Stop();
        var speedMBps = stream.Length / (1024.0 * 1024) / sw.Elapsed.TotalSeconds;

        _logger.LogInformation("Checksum computed: {Hash} in {Time:F2}s ({Speed:F0} MB/s)",
            checksum, sw.Elapsed.TotalSeconds, speedMBps);

        return checksum;
    }

    /// <summary>
    /// Verify that sender and receiver checksums match.
    /// </summary>
    public async Task<ChecksumResult> VerifyAsync(string filePath, string expectedChecksum, CancellationToken ct = default)
    {
        var actualChecksum = await ComputeChecksumAsync(filePath, ct);
        var match = string.Equals(actualChecksum, expectedChecksum, StringComparison.OrdinalIgnoreCase);

        var result = new ChecksumResult
        {
            FilePath = filePath,
            ExpectedChecksum = expectedChecksum,
            ActualChecksum = actualChecksum,
            IsMatch = match
        };

        if (match)
        {
            _logger.LogInformation("✅ Integrity check PASSED: {Path}", filePath);
        }
        else
        {
            _logger.LogError("❌ Integrity check FAILED: {Path}. Expected: {Expected}, Got: {Actual}",
                filePath, expectedChecksum, actualChecksum);
        }

        return result;
    }
}

/// <summary>
/// Result of an integrity check.
/// </summary>
public sealed class ChecksumResult
{
    public required string FilePath { get; init; }
    public required string ExpectedChecksum { get; init; }
    public required string ActualChecksum { get; init; }
    public required bool IsMatch { get; init; }
}
