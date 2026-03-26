namespace Shart.Core.Models;

/// <summary>
/// Represents metadata about a file being transferred.
/// </summary>
public sealed class FileMetadata
{
    /// <summary>Unique identifier for this file in the transfer session.</summary>
    public Guid FileId { get; init; } = Guid.NewGuid();

    /// <summary>Original file name including extension.</summary>
    public required string FileName { get; init; }

    /// <summary>Full source path on the sender's machine.</summary>
    public string? SourcePath { get; init; }

    /// <summary>Destination path on the receiver's machine.</summary>
    public string? DestinationPath { get; set; }

    /// <summary>Total file size in bytes.</summary>
    public required long FileSizeBytes { get; init; }

    /// <summary>MIME type of the file (e.g., "video/mp4").</summary>
    public string? MimeType { get; init; }

    /// <summary>Number of chunks this file is split into for parallel transfer.</summary>
    public int ChunkCount { get; set; }

    /// <summary>Size of each chunk in bytes (last chunk may be smaller).</summary>
    public long ChunkSizeBytes { get; set; }

    /// <summary>xxHash/CRC32 checksum of the complete file (set after transfer).</summary>
    public string? Checksum { get; set; }

    /// <summary>When the file was last modified on the source system.</summary>
    public DateTime? LastModifiedUtc { get; init; }

    /// <summary>Human-readable file size.</summary>
    public string FormattedSize => FormatBytes(FileSizeBytes);

    private static string FormatBytes(long bytes)
    {
        string[] sizes = ["B", "KB", "MB", "GB", "TB"];
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len /= 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
