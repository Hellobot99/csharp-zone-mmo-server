using System.Net.Sockets;
using System.Threading.Channels;
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
///   Packets are written to an unbounded Channel; a dedicated SendLoopAsync drains it.
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

    // Send – dedicated writer loop via Channel
    private readonly Channel<byte[]> _sendChannel = Channel.CreateUnbounded<byte[]>(
        new UnboundedChannelOptions { SingleReader = true, AllowSynchronousContinuations = false });

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

    }

    // ── Lifecycle ─────────────────────────────────────────────────────────────

    public void Start()
    {
        _ = SendLoopAsync();
        if (!_socket.ReceiveAsync(ReceiveSAEA))
            ProcessReceive(ReceiveSAEA);
    }

    public void Close()
    {
        if (Interlocked.CompareExchange(ref _isConnected, 0, 1) != 1) return;

        _sendChannel.Writer.TryComplete();
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
        _sendChannel.Writer.TryWrite(data);
    }

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var data in _sendChannel.Reader.ReadAllAsync())
            {
                if (!IsConnected) break;
                try
                {
                    await _socket.SendAsync(data, SocketFlags.None);
                }
                catch
                {
                    Close();
                    break;
                }
            }
        }
        catch { /* channel completed or socket closed */ }
    }
}
