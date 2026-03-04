using GameProto;
using GameServer.Game;
using GameServer.Network;

namespace GameServer.Packets.Handlers;

public sealed class PingPacketHandler : IPacketHandler
{
    private readonly SessionManager _sessions;

    public PingPacketHandler(SessionManager sessions)
    {
        _sessions = sessions;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var ps = _sessions.Get(session.SessionId);
        if (ps is not null)
            ps.LastPingAt = DateTime.UtcNow;

        session.Send(PacketHelper.Build(PacketType.Pong, new Pong()));
        return Task.CompletedTask;
    }
}
