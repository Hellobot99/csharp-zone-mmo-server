using GameServer.Network;

namespace GameServer.Packets.Handlers;

/// <summary>
/// Contract for all packet handlers.
/// Handlers are resolved per-request from the DI container (scoped lifetime).
/// </summary>
public interface IPacketHandler
{
    Task HandleAsync(IClientSession session, byte[] body);
}
