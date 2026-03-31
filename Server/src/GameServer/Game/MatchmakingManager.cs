using System.Collections.Concurrent;
using GameProto;
using GameServer.Packets;
using Microsoft.Extensions.Logging;

namespace GameServer.Game;

public class MatchmakingManager
{
    // 아레나 ZoneId 1~9
    private static readonly int[] ArenaZoneIds = Enumerable.Range(1, 9).ToArray();

    // 아레나별 스폰 위치
    private static readonly Dictionary<int, (float X1, float Y1, float X2, float Y2)> ArenaSpawns = new()
    {
        [1] = (  0f, 650f,   0f, 550f), [2] = (300f, 650f, 300f, 550f), [3] = (600f, 650f, 600f, 550f),
        [4] = (  0f, 350f,   0f, 250f), [5] = (300f, 350f, 300f, 250f), [6] = (600f, 350f, 600f, 250f),
        [7] = (  0f,  50f,   0f, -50f), [8] = (300f,  50f, 300f, -50f), [9] = (600f,  50f, 600f, -50f),
    };

    public const int LobbyZoneId = 10;
    public static readonly (float X, float Y) LobbySpawn = (-740f, 740f);

    private readonly ConcurrentQueue<PlayerSession> _queue = new();
    private readonly ConcurrentDictionary<int, (PlayerSession P1, PlayerSession P2)> _activeMatches = new(); // zoneId → match
    private readonly ZoneManager _zones;
    private readonly ILogger<MatchmakingManager> _logger;

    public MatchmakingManager(ZoneManager zones, ILogger<MatchmakingManager> logger)
    {
        _zones = zones;
        _logger = logger;
    }

    public void Enqueue(PlayerSession ps)
    {
        // 이미 대기중이거나 아레나에 있으면 무시
        if (ps.ZoneId != LobbyZoneId) return;
        if (_queue.Any(p => p.PlayerId == ps.PlayerId)) return;

        _queue.Enqueue(ps);
        ps.Connection.Send(PacketType.MatchmakeResponse, new MatchmakeResponse { Success = true, Message = "Queued" });
        _logger.LogInformation("[Matchmaking] Player {PlayerId} queued. Queue size: {Size}", ps.PlayerId, _queue.Count);

        TryMatch();
    }

    private void TryMatch()
    {
        if (_queue.Count < 2) return;

        // 빈 아레나 찾기
        int arenaId = FindFreeArena();
        if (arenaId == -1) return; // 아레나 꽉 참 — 큐에 그대로 대기

        if (!_queue.TryDequeue(out var p1)) return;
        if (!_queue.TryDequeue(out var p2))
        {
            _queue.Enqueue(p1);
            return;
        }

        var (spawnP1X, spawnP1Y, spawnP2X, spawnP2Y) = ArenaSpawns[arenaId];

        _activeMatches[arenaId] = (p1, p2);

        MoveToArena(p1, arenaId, spawnP1X, spawnP1Y, p2.PlayerId);
        MoveToArena(p2, arenaId, spawnP2X, spawnP2Y, p1.PlayerId);

        _logger.LogInformation("[Matchmaking] Match started in Zone {ZoneId}: Player {P1} vs Player {P2}", arenaId, p1.PlayerId, p2.PlayerId);
    }

    private void MoveToArena(PlayerSession ps, int arenaId, float spawnX, float spawnY, int opponentId)
    {
        _zones.Enter(ps, arenaId);
        ps.X = spawnX;
        ps.Y = spawnY;
        ps.Hp = ps.MaxHp;
        ps.IsInvincible = true;

        ps.Connection.Send(PacketType.MatchStarted, new MatchStarted
        {
            ZoneId = arenaId,
            OpponentId = opponentId,
            SpawnX = spawnX,
            SpawnY = spawnY,
        });

        // 3초 무적
        _ = Task.Delay(3000).ContinueWith(_ => ps.IsInvincible = false);
    }

    public void OnPlayerDied(PlayerSession dead, PlayerSession killer, int arenaId)
    {
        if (!_activeMatches.TryRemove(arenaId, out var match)) return;

        var winner = match.P1.PlayerId == killer.PlayerId ? match.P1 : match.P2;
        var loser  = match.P1.PlayerId == dead.PlayerId   ? match.P1 : match.P2;

        var endPacket = PacketHelper.Build(PacketType.MatchEnded, new MatchEnded
        {
            WinnerId = winner.PlayerId,
            LoserId  = loser.PlayerId,
        });

        winner.Connection.Send(endPacket);
        loser.Connection.Send(endPacket);

        _logger.LogInformation("[Matchmaking] Match ended in Zone {ZoneId}: Winner={W}, Loser={L}", arenaId, winner.PlayerId, loser.PlayerId);

        // 둘 다 로비로
        _ = Task.Delay(3000).ContinueWith(_ =>
        {
            MoveToLobby(winner);
            MoveToLobby(loser);
            TryMatch(); // 대기 중인 플레이어 매칭 시도
        });
    }

    public void MoveToLobby(PlayerSession ps)
    {
        ps.Hp = ps.MaxHp;
        ps.X = LobbySpawn.X;
        ps.Y = LobbySpawn.Y;
        _zones.Enter(ps, LobbyZoneId);

        ps.Connection.Send(PacketType.ZoneTransferResponse, new ZoneTransferResponse
        {
            Success = true,
            ZoneId  = LobbyZoneId,
            SpawnX  = LobbySpawn.X,
            SpawnY  = LobbySpawn.Y,
        });
    }

    private int FindFreeArena()
    {
        foreach (var id in ArenaZoneIds)
        {
            if (_activeMatches.ContainsKey(id)) continue;
            return id;
        }
        return -1;
    }

    public bool IsArenaMatch(int zoneId) => zoneId >= 1 && zoneId <= 9;
}
