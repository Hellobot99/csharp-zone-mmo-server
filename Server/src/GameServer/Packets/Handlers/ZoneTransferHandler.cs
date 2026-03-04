using GameProto;
using GameServer.Game;
using GameServer.Network;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

public sealed class ZoneTransferHandler : IPacketHandler
{
    // ZoneId → 스폰 위치 (3x3 그리드)
    // Zone1 Zone2 Zone3
    // Zone4 Zone5 Zone6
    // Zone7 Zone8 Zone9
    private static readonly Dictionary<int, (float X, float Y)> SpawnPoints = new()
    {
        [1] = (0f, 600f),
        [2] = (300f, 600f),
        [3] = (600f, 600f),
        [4] = (0f, 300f),
        [5] = (300f, 300f),
        [6] = (600f, 300f),
        [7] = (0f, 0f),
        [8] = (300f, 0f),
        [9] = (600f, 0f),
    };

    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;
    private readonly ILogger<ZoneTransferHandler> _logger;

    public ZoneTransferHandler(SessionManager sessions, ZoneManager zones, ILogger<ZoneTransferHandler> logger)
    {
        _sessions = sessions;
        _zones = zones;
        _logger = logger;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var req = ZoneTransferRequest.Parser.ParseFrom(body);
        var ps = _sessions.Get(session.SessionId);
        if (ps is null) return Task.CompletedTask;
        if (!SpawnPoints.TryGetValue(req.TargetZoneId, out var spawn)) return Task.CompletedTask;

        var fromZoneId = ps.ZoneId;

        // 1. 현재 존에서 퇴장 브로드캐스트 (옵저버 제외)
        var fromZone = _zones.GetOrCreate(fromZoneId);
        if (!ps.IsObserver)
            fromZone.Broadcast(PacketType.PlayerLeave,
                new PlayerLeave { PlayerId = ps.PlayerId },
                excludePlayerId: ps.PlayerId);

        // 2. 스폰 위치 갱신 후 새 존 입장
        ps.X = spawn.X;
        ps.Y = spawn.Y;
        _zones.Enter(ps, req.TargetZoneId);

        // 3. 새 존 기존 플레이어들에게 입장 알림 (옵저버 제외)
        var toZone = _zones.GetOrCreate(ps.ZoneId);
        if (!ps.IsObserver)
            toZone.Broadcast(PacketType.PlayerEnter,
                new PlayerEnter { PlayerId = ps.PlayerId, Username = ps.Username, X = ps.X, Y = ps.Y },
                excludePlayerId: ps.PlayerId);

        // 4. 클라에게 스폰 위치 응답
        session.Send(PacketType.ZoneTransferResponse,
            new ZoneTransferResponse { Success = true, ZoneId = ps.ZoneId, SpawnX = spawn.X, SpawnY = spawn.Y });

        return Task.CompletedTask;
    }
}
