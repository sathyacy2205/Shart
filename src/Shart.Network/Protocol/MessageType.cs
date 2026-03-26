namespace Shart.Network.Protocol;

/// <summary>
/// Types of messages exchanged over the control channel.
/// </summary>
public enum MessageType : byte
{
    /// <summary>Initial handshake to establish session.</summary>
    Handshake = 0x01,

    /// <summary>Response to handshake.</summary>
    HandshakeAck = 0x02,

    /// <summary>File metadata announcement (before transfer).</summary>
    FileAnnounce = 0x10,

    /// <summary>Accept file transfer.</summary>
    FileAccept = 0x11,

    /// <summary>Reject file transfer.</summary>
    FileReject = 0x12,

    /// <summary>Chunk assignment (which socket handles which chunk).</summary>
    ChunkAssignment = 0x20,

    /// <summary>Chunk data header (precedes raw bytes on data socket).</summary>
    ChunkHeader = 0x21,

    /// <summary>Chunk transfer complete acknowledgement.</summary>
    ChunkAck = 0x22,

    /// <summary>File transfer complete.</summary>
    FileComplete = 0x30,

    /// <summary>Checksum verification result.</summary>
    ChecksumVerify = 0x31,

    /// <summary>Pause transfer request.</summary>
    Pause = 0x40,

    /// <summary>Resume transfer request.</summary>
    Resume = 0x41,

    /// <summary>Cancel transfer request.</summary>
    Cancel = 0x42,

    /// <summary>Connection heartbeat ping.</summary>
    Heartbeat = 0xF0,

    /// <summary>Heartbeat acknowledgement.</summary>
    HeartbeatAck = 0xF1,

    /// <summary>Error notification.</summary>
    Error = 0xFF
}
