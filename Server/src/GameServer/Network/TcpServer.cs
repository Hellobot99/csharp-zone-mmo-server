using System.Net;
using System.Net.Sockets;
using GameServer.Config;
using GameServer.Packets.Handlers;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace GameServer.Network;

/// <summary>
/// High-performance TCP server based on SocketAsyncEventArgs (IOCP on Windows).
///
/// Design overview:
///  - A shared <see cref="BufferManager"/> pre-allocates one large byte[] split into
///    MaxConnections slices.  Each receive SAEA borrows one slice, so the managed
///    heap never gets pinned during I/O.
///  - A <see cref="SAEAPool"/> holds pre-created receive SAEA objects.
///  - When a client connects, one SAEA is taken from the pool and assigned to a new
///    <see cref="TcpSession"/>.  When the session closes the SAEA is returned.
///  - A <see cref="SemaphoreSlim"/> limits concurrent connections to MaxConnections.
/// </summary>
public sealed class TcpServer
{
    private readonly PacketHandlerManager _packetHandlerManager;
    private readonly ServerConfig _config;
    private readonly ILogger<TcpServer> _logger;

    private readonly BufferManager _bufferManager;
    private readonly SAEAPool _receivePool;
    private readonly SemaphoreSlim _connectionThrottle;

    private Socket? _listenSocket;
    private int _connectionCount;

    public TcpServer(
        PacketHandlerManager packetHandlerManager,
        IOptions<ServerConfig> config,
        ILogger<TcpServer> logger)
    {
        _packetHandlerManager = packetHandlerManager;
        _config = config.Value;
        _logger = logger;

        _connectionThrottle = new SemaphoreSlim(_config.MaxConnections, _config.MaxConnections);
        _bufferManager = new BufferManager(_config.MaxConnections * _config.BufferSize, _config.BufferSize);
        _receivePool = new SAEAPool();

        PreAllocate();
    }

    // ── Pre-allocation ────────────────────────────────────────────────────────

    private void PreAllocate()
    {
        for (int i = 0; i < _config.MaxConnections; i++)
        {
            var saea = new SocketAsyncEventArgs();
            saea.Completed += OnReceiveCompleted;
            _bufferManager.SetBuffer(saea);
            _receivePool.Push(saea);
        }
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        _listenSocket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        _listenSocket.NoDelay = true;
        _listenSocket.Bind(new IPEndPoint(IPAddress.Any, _config.Port));
        _listenSocket.Listen(_config.Backlog);

        _logger.LogInformation("TCP server listening on :{Port}  (max {Max} connections)",
            _config.Port, _config.MaxConnections);

        StartAccept(null);
    }

    public void Stop()
    {
        _listenSocket?.Close();
        _logger.LogInformation("TCP server stopped.");
    }

    // ── Accept loop ───────────────────────────────────────────────────────────

    private void StartAccept(SocketAsyncEventArgs? saea)
    {
        if (saea is null)
        {
            saea = new SocketAsyncEventArgs();
            saea.Completed += OnAcceptCompleted;
        }
        else
        {
            saea.AcceptSocket = null;
        }

        // Block if we are at max capacity. This keeps _listenSocket's backlog from overflowing.
        _connectionThrottle.Wait();

        if (!_listenSocket!.AcceptAsync(saea))
            ProcessAccept(saea);
    }

    private void OnAcceptCompleted(object? sender, SocketAsyncEventArgs e) => ProcessAccept(e);

    private void ProcessAccept(SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            _connectionThrottle.Release();
            _logger.LogError("Accept error: {Error}", e.SocketError);
            StartAccept(e);
            return;
        }

        var receiveSAEA = _receivePool.Pop();
        var session = new TcpSession(e.AcceptSocket!, receiveSAEA, _logger);
        receiveSAEA.UserToken = session;

        // Wire packet dispatch and disconnect cleanup
        session.PacketReceived += _packetHandlerManager.DispatchAsync;
        session.Disconnected += OnSessionDisconnected;

        Interlocked.Increment(ref _connectionCount);
        _logger.LogInformation("Client connected  [session={Id}]  total={Count}",
            session.SessionId, _connectionCount);

        session.Start();

        // Immediately post the next accept
        StartAccept(e);
    }

    // ── I/O completion callbacks ──────────────────────────────────────────────

    /// <summary>Receive completions are routed here by the shared SAEA.</summary>
    private void OnReceiveCompleted(object? sender, SocketAsyncEventArgs e)
    {
        ((TcpSession)e.UserToken!).ProcessReceive(e);
    }

    private void OnSessionDisconnected(TcpSession session)
    {
        // Return the receive SAEA to the pool so it can be reused
        _receivePool.Push(session.ReceiveSAEA);
        _connectionThrottle.Release();

        Interlocked.Decrement(ref _connectionCount);
        _logger.LogInformation("Client disconnected [session={Id}]  total={Count}",
            session.SessionId, _connectionCount);
    }
}
