using GameProto;
using GameServer.Game;
using GameServer.Network;

namespace GameServer.Packets.Handlers;

public sealed class RequestSnapshotHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;

    public RequestSnapshotHandler(SessionManager sessions, ZoneManager zones)
    {
        _sessions = sessions;
        _zones = zones;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var ps = _sessions.Get(session.SessionId);
        if (ps is null) return Task.CompletedTask;

        var snapshot = new ZoneSnapshot();
        var players = ps.IsObserver
            ? _zones.GetAllPlayers()
            : _zones.GetOrCreate(ps.ZoneId).GetAllPlayers();
        foreach (var existing in players)
        {
            if (existing.IsObserver) continue;
            snapshot.Players.Add(new PlayerEnter { PlayerId = existing.PlayerId, Username = existing.Username, X = existing.X, Y = existing.Y });
        }

        session.Send(PacketType.ZoneSnapshot, snapshot);
        return Task.CompletedTask;
    }
}
