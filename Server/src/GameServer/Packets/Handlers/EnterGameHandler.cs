using GameProto;
using GameServer.Game;
using GameServer.Network;

namespace GameServer.Packets.Handlers;

public sealed class EnterGameHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;

    public EnterGameHandler(SessionManager sessions, ZoneManager zones)
    {
        _sessions = sessions;
        _zones = zones;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var ps = _sessions.Get(session.SessionId);
        if (ps is null || ps.IsObserver) return Task.CompletedTask;

        session.Send(PacketType.SpawnResponse, new SpawnResponse { X = ps.X, Y = ps.Y, ZoneId = ps.ZoneId });

        var zone = _zones.GetOrCreate(ps.ZoneId);
        zone.Broadcast(PacketType.PlayerEnter,
            new PlayerEnter { PlayerId = ps.PlayerId, Username = ps.Username, X = ps.X, Y = ps.Y },
            excludePlayerId: ps.PlayerId);

        return Task.CompletedTask;
    }
}
