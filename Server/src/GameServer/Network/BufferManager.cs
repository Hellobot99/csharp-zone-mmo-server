using System.Net.Sockets;

namespace GameServer.Network;

/// <summary>
/// Manages a single large pinned byte[] that is sliced into fixed-size regions,
/// one per SocketAsyncEventArgs.  Keeping all receive buffers in one allocation
/// prevents GC from fragmenting the LOH and avoids repeated pinning overhead.
/// </summary>
public sealed class BufferManager
{
    private readonly int _bufferSize;
    private byte[] _buffer = null!;
    private int _currentIndex;
    private readonly Stack<int> _freeSlots = new();

    public BufferManager(int totalBytes, int bufferSize)
    {
        _bufferSize = bufferSize;
        _buffer = new byte[totalBytes];
    }

    /// <summary>
    /// Assigns a buffer slice to <paramref name="args"/>.
    /// Returns false if the pool is exhausted.
    /// </summary>
    public bool SetBuffer(SocketAsyncEventArgs args)
    {
        int offset;
        if (_freeSlots.Count > 0)
        {
            offset = _freeSlots.Pop();
        }
        else
        {
            if (_currentIndex + _bufferSize > _buffer.Length) return false;
            offset = _currentIndex;
            _currentIndex += _bufferSize;
        }

        args.SetBuffer(_buffer, offset, _bufferSize);
        return true;
    }

    /// <summary>Returns the buffer slice used by <paramref name="args"/> back to the pool.</summary>
    public void FreeBuffer(SocketAsyncEventArgs args)
    {
        _freeSlots.Push(args.Offset);
        args.SetBuffer(null, 0, 0);
    }
}
