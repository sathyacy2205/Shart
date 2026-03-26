using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Shart.Network.Protocol;

namespace Shart.Network.Transport;

/// <summary>
/// Manages a pool of parallel TCP data sockets alongside a control socket.
/// This is the core multiplexing engine that enables gigabit-class speeds
/// by spreading data across 4-8 concurrent TCP streams.
/// </summary>
public sealed class ConnectionMultiplexer : IAsyncDisposable
{
    private readonly ILogger<ConnectionMultiplexer> _logger;
    private TcpClient? _controlSocket;
    private NetworkStream? _controlStream;
    private readonly List<TcpClient> _dataSockets = [];
    private readonly List<NetworkStream> _dataStreams = [];
    private readonly object _lock = new();

    /// <summary>Number of parallel data streams.</summary>
    public int DataStreamCount { get; init; } = 4;

    /// <summary>Base port for the control channel.</summary>
    public int ControlPort { get; init; } = 9876;

    /// <summary>Starting port for data channels (sequential from this).</summary>
    public int DataPortBase { get; init; } = 9877;

    /// <summary>TCP send/receive buffer size (256KB for high throughput).</summary>
    public int SocketBufferSize { get; init; } = 256 * 1024;

    /// <summary>Whether the multiplexer is connected.</summary>
    public bool IsConnected => _controlSocket?.Connected ?? false;

    public ConnectionMultiplexer(ILogger<ConnectionMultiplexer> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Connect to a remote device as client.
    /// Opens 1 control socket + N data sockets in parallel.
    /// </summary>
    public async Task ConnectAsync(IPAddress remoteAddress, CancellationToken ct = default)
    {
        _logger.LogInformation("Connecting to {IP} — 1 control + {N} data sockets",
            remoteAddress, DataStreamCount);

        // Connect control socket
        _controlSocket = CreateOptimizedTcpClient();
        await _controlSocket.ConnectAsync(remoteAddress, ControlPort, ct);
        _controlStream = _controlSocket.GetStream();

        _logger.LogInformation("Control channel connected on port {Port}", ControlPort);

        // Connect data sockets in parallel
        var connectTasks = new List<Task>();
        for (int i = 0; i < DataStreamCount; i++)
        {
            int port = DataPortBase + i;
            connectTasks.Add(ConnectDataSocketAsync(remoteAddress, port, ct));
        }

        await Task.WhenAll(connectTasks);
        _logger.LogInformation("All {N} data channels connected", DataStreamCount);
    }

    /// <summary>
    /// Start listening for incoming connections as server.
    /// </summary>
    public async Task<ConnectionMultiplexer> AcceptAsync(IPAddress localAddress, CancellationToken ct = default)
    {
        _logger.LogInformation("Listening on {IP} — 1 control + {N} data ports",
            localAddress, DataStreamCount);

        // Listen on control port
        var controlListener = new TcpListener(localAddress, ControlPort);
        controlListener.Start();
        _controlSocket = await controlListener.AcceptTcpClientAsync(ct);
        ConfigureSocket(_controlSocket);
        _controlStream = _controlSocket.GetStream();
        controlListener.Stop();

        _logger.LogInformation("Control channel accepted on port {Port}", ControlPort);

        // Listen on data ports in parallel
        var acceptTasks = new List<Task>();
        for (int i = 0; i < DataStreamCount; i++)
        {
            int port = DataPortBase + i;
            acceptTasks.Add(AcceptDataSocketAsync(localAddress, port, ct));
        }

        await Task.WhenAll(acceptTasks);
        _logger.LogInformation("All {N} data channels accepted", DataStreamCount);

        return this;
    }

    /// <summary>
    /// Send a protocol message over the control channel.
    /// </summary>
    public async Task SendControlMessageAsync(ProtocolMessage message, CancellationToken ct = default)
    {
        if (_controlStream == null) throw new InvalidOperationException("Control channel not connected.");

        var bytes = ProtocolSerializer.Serialize(message);
        await _controlStream.WriteAsync(bytes, ct);
        await _controlStream.FlushAsync(ct);
    }

    /// <summary>
    /// Receive a protocol message from the control channel.
    /// </summary>
    public async Task<ProtocolMessage?> ReceiveControlMessageAsync(CancellationToken ct = default)
    {
        if (_controlStream == null) throw new InvalidOperationException("Control channel not connected.");
        return await ProtocolSerializer.DeserializeAsync(_controlStream, ct);
    }

    /// <summary>
    /// Get a specific data stream by index (for the StreamOrchestrator to use).
    /// </summary>
    public NetworkStream GetDataStream(int index)
    {
        lock (_lock)
        {
            if (index < 0 || index >= _dataStreams.Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return _dataStreams[index];
        }
    }

    /// <summary>
    /// Get all active data streams.
    /// </summary>
    public IReadOnlyList<NetworkStream> GetAllDataStreams()
    {
        lock (_lock) { return _dataStreams.ToList(); }
    }

    /// <summary>
    /// Write raw data to a specific data stream.
    /// </summary>
    public async Task WriteDataAsync(int streamIndex, ReadOnlyMemory<byte> data, CancellationToken ct = default)
    {
        var stream = GetDataStream(streamIndex);
        await stream.WriteAsync(data, ct);
    }

    /// <summary>
    /// Read raw data from a specific data stream.
    /// </summary>
    public async Task<int> ReadDataAsync(int streamIndex, Memory<byte> buffer, CancellationToken ct = default)
    {
        var stream = GetDataStream(streamIndex);
        return await stream.ReadAsync(buffer, ct);
    }

    private TcpClient CreateOptimizedTcpClient()
    {
        var client = new TcpClient();
        ConfigureSocket(client);
        return client;
    }

    private void ConfigureSocket(TcpClient client)
    {
        client.NoDelay = true; // Disable Nagle's algorithm for low latency
        client.SendBufferSize = SocketBufferSize;
        client.ReceiveBufferSize = SocketBufferSize;
        client.LingerState = new LingerOption(true, 10);
    }

    private async Task ConnectDataSocketAsync(IPAddress remoteAddress, int port, CancellationToken ct)
    {
        var client = CreateOptimizedTcpClient();
        await client.ConnectAsync(remoteAddress, port, ct);

        lock (_lock)
        {
            _dataSockets.Add(client);
            _dataStreams.Add(client.GetStream());
        }

        _logger.LogDebug("Data channel connected on port {Port}", port);
    }

    private async Task AcceptDataSocketAsync(IPAddress localAddress, int port, CancellationToken ct)
    {
        var listener = new TcpListener(localAddress, port);
        listener.Start();

        var client = await listener.AcceptTcpClientAsync(ct);
        ConfigureSocket(client);
        listener.Stop();

        lock (_lock)
        {
            _dataSockets.Add(client);
            _dataStreams.Add(client.GetStream());
        }

        _logger.LogDebug("Data channel accepted on port {Port}", port);
    }

    public async ValueTask DisposeAsync()
    {
        _controlStream?.Dispose();
        _controlSocket?.Dispose();

        lock (_lock)
        {
            foreach (var stream in _dataStreams) stream.Dispose();
            foreach (var socket in _dataSockets) socket.Dispose();
            _dataStreams.Clear();
            _dataSockets.Clear();
        }

        _logger.LogInformation("ConnectionMultiplexer disposed.");
    }
}
