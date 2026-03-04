using System.Collections.Concurrent;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;

namespace GameServer.Network;

/// <summary>
/// Represents a single connected client.
///
/// Packet framing protocol (little-endian):
///   [TotalSize : UInt16][PacketType : UInt16][Body : TotalSize-4 bytes]
///
/// Receive path:
///   Raw bytes land in the shared receive SAEA buffer → copied into a per-session
///   assembly buffer → complete packets are extracted and fired via PacketReceived.
///
/// Send path:
///   Callers enqueue byte[] payloads.  A lock-free flag (_isSending) ensures only
///   one send operation is in flight at a time; remaining items drain in ProcessSend.
/// </summary>
public sealed class TcpSession : IClientSession
{
    // ── Constants ─────────────────────────────────────────────────────────────
    private const int HeaderSize = 4;           // UInt16 size + UInt16 type
    private const int AssemblyBufferSize = 65536; // 64 KB per session

    // ── State ─────────────────────────────────────────────────────────────────
    private readonly Socket _socket;
    private readonly ILogger _logger;

    // Receive
    private readonly byte[] _assemblyBuffer = new byte[AssemblyBufferSize];
    private int _assemblyPos;

    // Send – serialised via _isSending (0 = idle, 1 = in-flight)
    private readonly ConcurrentQueue<byte[]> _sendQueue = new();
    private readonly SocketAsyncEventArgs _sendSAEA;
    private int _isSending;

    // Connection state
    private int _isConnected = 1; // 1 = connected

    // ── Public surface ────────────────────────────────────────────────────────
    public int SessionId { get; }
    public bool IsConnected => Volatile.Read(ref _isConnected) == 1;

    /// <summary>The shared receive SAEA; returned to the pool on disconnect.</summary>
    public SocketAsyncEventArgs ReceiveSAEA { get; }

    /// <summary>Fired for every complete inbound packet.</summary>
    public event Func<IClientSession, ushort, byte[], Task>? PacketReceived;

    /// <summary>Fired once when the session is torn down.</summary>
    public event Action<TcpSession>? Disconnected;

    // ── Constructor ───────────────────────────────────────────────────────────
    public TcpSession(Socket socket, SocketAsyncEventArgs receiveSAEA, ILogger logger)
    {
        _socket = socket;
        ReceiveSAEA = receiveSAEA;
        _logger = logger;
        SessionId = socket.GetHashCode();

        _sendSAEA = new SocketAsyncEventArgs();
        _sendSAEA.Completed += OnSendCompleted;
        _sendSAEA.UserToken = this;
    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        if (!_socket.ReceiveAsync(ReceiveSAEA))
            ProcessReceive(ReceiveSAEA);
    }

    public void Close()
    {
        if (Interlocked.CompareExchange(ref _isConnected, 0, 1) != 1) return; // already closed

        try { _socket.Shutdown(SocketShutdown.Both); } catch { }
        try { _socket.Close(); } catch { }

        Disconnected?.Invoke(this);
    }

    // ── Receive ───────────────────────────────────────────────────────────────

    public void ProcessReceive(SocketAsyncEventArgs e)
    {
        if (!IsConnected) return;

        if (e.SocketError != SocketError.Success || e.BytesTransferred == 0)
        {
            Close();
            return;
        }

        // Guard against assembly-buffer overflow
        if (_assemblyPos + e.BytesTransferred > AssemblyBufferSize)
        {
            _logger.LogWarning("[{Id}] Assembly buffer overflow closing session.", SessionId);
            Close();
            return;
        }

        Buffer.BlockCopy(e.Buffer!, e.Offset, _assemblyBuffer, _assemblyPos, e.BytesTransferred);
        _assemblyPos += e.BytesTransferred;

        int consumed = 0;
        while (_assemblyPos - consumed >= HeaderSize)
        {
            ushort packetSize = BitConverter.ToUInt16(_assemblyBuffer, consumed);

            // Sanity-check the declared size
            if (packetSize < HeaderSize || packetSize > AssemblyBufferSize)
            {
                _logger.LogWarning("[{Id}] Invalid packet size {Size} – closing session.", SessionId, packetSize);
                Close();
                return;
            }

            if (_assemblyPos - consumed < packetSize) break; // wait for more data

            ushort packetType = BitConverter.ToUInt16(_assemblyBuffer, consumed + 2);
            int bodyLen = packetSize - HeaderSize;
            byte[] body = bodyLen > 0 ? _assemblyBuffer.AsSpan(consumed + HeaderSize, bodyLen).ToArray() : [];

            // Dispatch off the receive thread to avoid blocking the I/O loop
            FirePacketReceived(packetType, body);

            consumed += packetSize;
        }

        // Compact the assembly buffer
        if (consumed > 0)
        {
            int remaining = _assemblyPos - consumed;
            if (remaining > 0)
                Buffer.BlockCopy(_assemblyBuffer, consumed, _assemblyBuffer, 0, remaining);
            _assemblyPos = remaining;
        }

        // Post the next receive
        if (!_socket.ReceiveAsync(e))
            ProcessReceive(e);
    }

    private void FirePacketReceived(ushort packetType, byte[] body)
    {
        if (PacketReceived is null) return;
        _ = Task.Run(async () =>
        {
            try { await PacketReceived(this, packetType, body); }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[{Id}] Unhandled exception in packet handler (type=0x{Type:X4}).",
                    SessionId, packetType);
            }
        });
    }

    // ── Send ──────────────────────────────────────────────────────────────────

    public void Send(byte[] data)
    {
        if (!IsConnected) return;
        _sendQueue.Enqueue(data);
        TryFlushSend();
    }

    private void TryFlushSend()
    {
        // Only one fiber enters the send path at a time
        if (Interlocked.CompareExchange(ref _isSending, 1, 0) != 0) return;

        if (_sendQueue.TryDequeue(out var data))
        {
            DoSend(data);
        }
        else
        {
            Interlocked.Exchange(ref _isSending, 0);
            // Re-check to close the TOCTOU gap
            if (!_sendQueue.IsEmpty) TryFlushSend();
        }
    }

    private void DoSend(byte[] data)
    {
        _sendSAEA.SetBuffer(data, 0, data.Length);
        if (!_socket.SendAsync(_sendSAEA))
            ProcessSend(_sendSAEA);
    }

    private void OnSendCompleted(object? sender, SocketAsyncEventArgs e) => ProcessSend(e);

    public void ProcessSend(SocketAsyncEventArgs e)
    {
        if (e.SocketError != SocketError.Success)
        {
            Interlocked.Exchange(ref _isSending, 0);
            Close();
            return;
        }

        if (_sendQueue.TryDequeue(out var next))
        {
            DoSend(next);
        }
        else
        {
            Interlocked.Exchange(ref _isSending, 0);
            if (!_sendQueue.IsEmpty) TryFlushSend();
        }
    }
}
