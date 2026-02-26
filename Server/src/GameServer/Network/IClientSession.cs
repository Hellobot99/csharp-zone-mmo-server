namespace GameServer.Network;

/// <summary>
/// Abstraction for a connected client session.
/// Packet handlers depend on this interface, not on the concrete TcpSession,
/// so the network layer remains decoupled from game logic.
/// </summary>
public interface IClientSession
{
    int SessionId { get; }
    bool IsConnected { get; }

    /// <summary>Enqueues a raw packet byte array for sending.</summary>
    void Send(byte[] data);

    /// <summary>Gracefully closes the connection.</summary>
    void Close();
}
