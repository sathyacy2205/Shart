using System.Buffers.Binary;
using System.Text;
using System.Text.Json;

namespace Shart.Network.Protocol;

/// <summary>
/// Binary protocol message format:
/// [1 byte MessageType] [4 bytes PayloadLength] [N bytes Payload]
/// </summary>
public sealed class ProtocolMessage
{
    public MessageType Type { get; init; }
    public byte[] Payload { get; init; } = [];

    /// <summary>Total wire size: 1 (type) + 4 (length) + payload.</summary>
    public int WireSize => 1 + 4 + Payload.Length;
}

/// <summary>
/// Serializes and deserializes protocol messages for the control channel.
/// Uses a simple binary format optimized for speed over readability.
/// </summary>
public static class ProtocolSerializer
{
    private const int HeaderSize = 5; // 1 byte type + 4 bytes length

    /// <summary>
    /// Serialize a protocol message to bytes.
    /// </summary>
    public static byte[] Serialize(ProtocolMessage message)
    {
        var buffer = new byte[HeaderSize + message.Payload.Length];
        buffer[0] = (byte)message.Type;
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(1), message.Payload.Length);

        if (message.Payload.Length > 0)
        {
            message.Payload.CopyTo(buffer.AsSpan(HeaderSize));
        }

        return buffer;
    }

    /// <summary>
    /// Deserialize a protocol message from a stream.
    /// </summary>
    public static async Task<ProtocolMessage?> DeserializeAsync(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[HeaderSize];
        int bytesRead = await ReadExactlyAsync(stream, header, HeaderSize, ct);

        if (bytesRead < HeaderSize) return null;

        var type = (MessageType)header[0];
        var payloadLength = BinaryPrimitives.ReadInt32LittleEndian(header.AsSpan(1));

        byte[] payload = [];
        if (payloadLength > 0)
        {
            payload = new byte[payloadLength];
            bytesRead = await ReadExactlyAsync(stream, payload, payloadLength, ct);
            if (bytesRead < payloadLength) return null;
        }

        return new ProtocolMessage { Type = type, Payload = payload };
    }

    /// <summary>
    /// Create a handshake message.
    /// </summary>
    public static ProtocolMessage CreateHandshake(string deviceName, string deviceId, string pin)
    {
        var data = new { DeviceName = deviceName, DeviceId = deviceId, Pin = pin, ProtocolVersion = 1 };
        return new ProtocolMessage
        {
            Type = MessageType.Handshake,
            Payload = JsonSerializer.SerializeToUtf8Bytes(data)
        };
    }

    /// <summary>
    /// Create a file announcement message.
    /// </summary>
    public static ProtocolMessage CreateFileAnnounce(Guid fileId, string fileName, long fileSize, int chunkCount, long chunkSize)
    {
        var data = new
        {
            FileId = fileId,
            FileName = fileName,
            FileSize = fileSize,
            ChunkCount = chunkCount,
            ChunkSize = chunkSize
        };
        return new ProtocolMessage
        {
            Type = MessageType.FileAnnounce,
            Payload = JsonSerializer.SerializeToUtf8Bytes(data)
        };
    }

    /// <summary>
    /// Create a chunk header message (sent before raw chunk data on a data socket).
    /// </summary>
    public static ProtocolMessage CreateChunkHeader(Guid fileId, int chunkIndex, long offset, int length)
    {
        var buffer = new byte[32]; // Guid(16) + ChunkIndex(4) + Offset(8) + Length(4)
        fileId.TryWriteBytes(buffer);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(16), chunkIndex);
        BinaryPrimitives.WriteInt64LittleEndian(buffer.AsSpan(20), offset);
        BinaryPrimitives.WriteInt32LittleEndian(buffer.AsSpan(28), length);

        return new ProtocolMessage
        {
            Type = MessageType.ChunkHeader,
            Payload = buffer
        };
    }

    /// <summary>
    /// Parse a chunk header from a protocol message payload.
    /// </summary>
    public static (Guid FileId, int ChunkIndex, long Offset, int Length) ParseChunkHeader(byte[] payload)
    {
        var fileId = new Guid(payload.AsSpan(0, 16));
        var chunkIndex = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(16));
        var offset = BinaryPrimitives.ReadInt64LittleEndian(payload.AsSpan(20));
        var length = BinaryPrimitives.ReadInt32LittleEndian(payload.AsSpan(28));
        return (fileId, chunkIndex, offset, length);
    }

    /// <summary>
    /// Create a heartbeat message.
    /// </summary>
    public static ProtocolMessage CreateHeartbeat()
    {
        return new ProtocolMessage
        {
            Type = MessageType.Heartbeat,
            Payload = BitConverter.GetBytes(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
        };
    }

    /// <summary>
    /// Create an error message.
    /// </summary>
    public static ProtocolMessage CreateError(string errorMessage)
    {
        return new ProtocolMessage
        {
            Type = MessageType.Error,
            Payload = Encoding.UTF8.GetBytes(errorMessage)
        };
    }

    private static async Task<int> ReadExactlyAsync(Stream stream, byte[] buffer, int count, CancellationToken ct)
    {
        int totalRead = 0;
        while (totalRead < count)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(totalRead, count - totalRead), ct);
            if (read == 0) break; // Stream ended
            totalRead += read;
        }
        return totalRead;
    }
}
