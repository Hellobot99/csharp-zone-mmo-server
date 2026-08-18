using System.Collections.Concurrent;
using GameProto;
using GameServer.Packets;
using Microsoft.Extensions.Logging;

namespace GameServer.Game;

public class MatchmakingManager
{
    private const int PlayersPerMatch = 4;
    private static readonly TimeSpan MatchTimeout = TimeSpan.FromSeconds(30);

    private static readonly int[] ArenaZoneIds = Enumerable.Range(1, 9).ToArray();

    // 아레나별 4개 스폰 위치 (좌상, 우상, 좌하, 우하)
    private static readonly Dictionary<int, (float X, float Y)[]> ArenaSpawns = new()
    {
        [1] = [(-80f, 680f), (80f, 680f), (-80f, 520f), (80f, 520f)],
        [2] = [(220f, 680f), (380f, 680f), (220f, 520f), (380f, 520f)],
        [3] = [(520f, 680f), (680f, 680f), (520f, 520f), (680f, 520f)],
        [4] = [(-80f, 380f), (80f, 380f), (-80f, 220f), (80f, 220f)],
        [5] = [(220f, 380f), (380f, 380f), (220f, 220f), (380f, 220f)],
        [6] = [(520f, 380f), (680f, 380f), (520f, 220f), (680f, 220f)],
        [7] = [(-80f,  80f), (80f,  80f), (-80f, -80f), (80f, -80f)],
        [8] = [(220f,  80f), (380f,  80f), (220f, -80f), (380f, -80f)],
        [9] = [(520f,  80f), (680f,  80f), (520f, -80f), (680f, -80f)],
    };

    public const int LobbyZoneId = 10;
    public static readonly (float X, float Y) LobbySpawn = (-740f, 320f);

    private readonly ConcurrentQueue<PlayerSession> _queue = new();
    private readonly ConcurrentDictionary<int, PlayerSession[]> _activeMatches = new();
    private readonly ZoneManager _zones;
    private readonly ILogger<MatchmakingManager> _logger;

    public MatchmakingManager(ZoneManager zones, ILogger<MatchmakingManager> logger)
    {
        _zones = zones;
        _logger = logger;
    }

    public void Enqueue(PlayerSession ps)
    {
        if (ps.ZoneId != LobbyZoneId) return;
        if (_queue.Any(p => p.PlayerId == ps.PlayerId)) return;

        _queue.Enqueue(ps);
        ps.Connection.Send(PacketType.MatchmakeResponse, new MatchmakeResponse { Success = true, Message = "Queued" });
        _logger.LogInformation("[Matchmaking] Player {Id} queued. Queue size: {Size}", ps.PlayerId, _queue.Count);

        TryMatch();
    }

    private void TryMatch()
    {
        if (_queue.Count < PlayersPerMatch) return;

        int arenaId = FindFreeArena();
        if (arenaId == -1) return;

        var players = new PlayerSession[PlayersPerMatch];
        for (int i = 0; i < PlayersPerMatch; i++)
        {
            if (!_queue.TryDequeue(out players[i]!))
            {
                // 충분하지 않으면 뽑은 것들 다시 넣기
                for (int j = 0; j < i; j++) _queue.Enqueue(players[j]);
                return;
            }
        }

        _activeMatches[arenaId] = players;

        var spawns = ArenaSpawns[arenaId];
        for (int i = 0; i < PlayersPerMatch; i++)
            MoveToArena(players[i], arenaId, spawns[i].X, spawns[i].Y);

        _logger.LogInformation("[Matchmaking] Match started in Zone {ZoneId} with {N} players", arenaId, PlayersPerMatch);

        // 30초 타임아웃
        _ = Task.Delay(MatchTimeout).ContinueWith(_ => ForceEndMatch(arenaId));
    }

    private void MoveToArena(PlayerSession ps, int arenaId, float spawnX, float spawnY)
    {
        _zones.GetOrCreate(ps.ZoneId).Broadcast(PacketType.PlayerLeave,
            new GameProto.PlayerLeave { PlayerId = ps.PlayerId });

        _zones.Enter(ps, arenaId);
        ps.X = spawnX;
        ps.Y = spawnY;
        ps.Hp = ps.MaxHp;
        ps.IsInvincible = true;

        _zones.GetOrCreate(arenaId).Broadcast(PacketType.PlayerEnter,
            new GameProto.PlayerEnter { PlayerId = ps.PlayerId, Username = ps.Username, X = spawnX, Y = spawnY },
            excludePlayerId: ps.PlayerId);

        ps.Connection.Send(PacketType.MatchStarted, new MatchStarted
        {
            ZoneId = arenaId,
            SpawnX = spawnX,
            SpawnY = spawnY,
        });

        _ = Task.Delay(3000).ContinueWith(_ => ps.IsInvincible = false);
    }

    public void OnPlayerDied(PlayerSession dead, PlayerSession killer, int arenaId)
    {
        if (!_activeMatches.TryGetValue(arenaId, out var players)) return;

        // 죽은 플레이어 즉시 로비로
        var endForDead = PacketHelper.Build(PacketType.MatchEnded, new MatchEnded { WinnerId = killer.PlayerId, LoserId = dead.PlayerId });
        dead.Connection.Send(endForDead);
        _ = Task.Delay(3000).ContinueWith(_ => MoveToLobby(dead));

        // 생존자 확인
        var alive = players.Where(p => !p.IsDead).ToArray();
        if (alive.Length == 1)
        {
            _activeMatches.TryRemove(arenaId, out _);
            var winner = alive[0];
            var endForWinner = PacketHelper.Build(PacketType.MatchEnded, new MatchEnded { WinnerId = winner.PlayerId, LoserId = dead.PlayerId });
            winner.Connection.Send(endForWinner);
            _ = Task.Delay(3000).ContinueWith(_ => { MoveToLobby(winner); TryMatch(); });
            _logger.LogInformation("[Matchmaking] Match ended in Zone {ZoneId}: Winner={W}", arenaId, winner.PlayerId);
        }
    }

    private void ForceEndMatch(int arenaId)
    {
        if (!_activeMatches.TryRemove(arenaId, out var players)) return;

        _logger.LogInformation("[Matchmaking] Match in Zone {ZoneId} timed out — forcing all to lobby", arenaId);

        var endPacket = PacketHelper.Build(PacketType.MatchEnded, new MatchEnded { WinnerId = 0, LoserId = 0 });
        foreach (var ps in players)
        {
            ps.Connection.Send(endPacket);
            MoveToLobby(ps);
        }
        TryMatch();
    }

    public void MoveToLobby(PlayerSession ps)
    {
        _zones.GetOrCreate(ps.ZoneId).Broadcast(PacketType.PlayerLeave,
            new GameProto.PlayerLeave { PlayerId = ps.PlayerId });

        ps.Hp = ps.MaxHp;
        ps.IsInvincible = true;
        _ = Task.Delay(3000).ContinueWith(_ => ps.IsInvincible = false);
        ps.X = LobbySpawn.X;
        ps.Y = LobbySpawn.Y;
        _zones.Enter(ps, LobbyZoneId);

        _zones.GetOrCreate(LobbyZoneId).Broadcast(PacketType.PlayerEnter,
            new GameProto.PlayerEnter { PlayerId = ps.PlayerId, Username = ps.Username, X = ps.X, Y = ps.Y },
            excludePlayerId: ps.PlayerId);

        ps.Connection.Send(PacketType.ZoneTransferResponse, new ZoneTransferResponse
        {
            Success = true,
            ZoneId = LobbyZoneId,
            SpawnX = LobbySpawn.X,
            SpawnY = LobbySpawn.Y,
        });
    }

    public void OnPlayerDisconnected(PlayerSession ps)
    {
        var entry = _activeMatches.FirstOrDefault(m => m.Value.Any(p => p.PlayerId == ps.PlayerId));
        if (entry.Value is null) return;

        var alive = entry.Value.Where(p => p.PlayerId != ps.PlayerId && !p.IsDead).ToArray();
        if (_activeMatches.TryRemove(entry.Key, out _))
        {
            if (alive.Length == 1)
            {
                var winner = alive[0];
                winner.Connection.Send(PacketHelper.Build(PacketType.MatchEnded, new MatchEnded { WinnerId = winner.PlayerId }));
                _ = Task.Delay(3000).ContinueWith(_ => { MoveToLobby(winner); TryMatch(); });
            }
            else
            {
                foreach (var survivor in alive) MoveToLobby(survivor);
                TryMatch();
            }
        }
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
