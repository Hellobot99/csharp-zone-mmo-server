using System.Net.Sockets;

namespace GameServer.Network;

/// <summary>
/// Thread-safe pool of <see cref="SocketAsyncEventArgs"/> instances.
/// Pre-allocating SAEA objects prevents per-operation GC pressure.
/// </summary>
public sealed class SAEAPool
{
    private readonly Stack<SocketAsyncEventArgs> _pool = new();

    public int Count
    {
        get { lock (_pool) return _pool.Count; }
    }

    public void Push(SocketAsyncEventArgs item)
    {
        ArgumentNullException.ThrowIfNull(item);
        lock (_pool) _pool.Push(item);
    }

    public SocketAsyncEventArgs Pop()
    {
        lock (_pool) return _pool.Pop();
    }
}
