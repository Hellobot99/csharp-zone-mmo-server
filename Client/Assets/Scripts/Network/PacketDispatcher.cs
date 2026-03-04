using System;
using System.Collections.Generic;
using UnityEngine;

public class PacketDispatcher
{
    private readonly Dictionary<ushort, Action<byte[]>> _handlers = new();

    public void Register(ushort packetType, Action<byte[]> handler)
    {
        _handlers[packetType] = handler;
    }

    public void Dispatch(ushort packetType, byte[] body)
    {
        if (_handlers.TryGetValue(packetType, out var handler))
            handler(body);
        else
            Debug.LogWarning($"[PacketDispatcher] No handler for packet type 0x{packetType:X4}");
    }
}
