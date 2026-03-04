using GameProto;
using GameServer.Game;
using GameServer.Network;

namespace GameServer.Packets.Handlers;

public sealed class MovePacketHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;

    public MovePacketHandler(SessionManager sessions, ZoneManager zones)
    {
        _sessions = sessions;
        _zones = zones;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        MoveRequest request;
        try { request = MoveRequest.Parser.ParseFrom(body); }
        catch { return Task.CompletedTask; }

        var ps = _sessions.Get(session.SessionId);
        if (ps is null) return Task.CompletedTask;

        ps.X = request.X;
        ps.Y = request.Y;

        var zone = _zones.GetOrCreate(ps.ZoneId);
        var packet = PacketHelper.Build(PacketType.MoveResponse,
            new MoveResponse { PlayerId = ps.PlayerId, X = request.X, Y = request.Y, Z = request.Z });

        // 다른 플레이어에게 먼저 전송 후, 송신자에게 마지막으로 전송
        // → 송신자의 RTT = 존 내 전체 브로드캐스트 완료까지의 최대 지연 측정
        zone.Broadcast(packet, excludePlayerId: ps.PlayerId);
        session.Send(packet);

        return Task.CompletedTask;
    }
}
