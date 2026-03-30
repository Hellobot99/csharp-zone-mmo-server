using GameProto;
using GameServer.Game;
using GameServer.Network;

namespace GameServer.Packets.Handlers;

public sealed class MovePacketHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;

    private const float AttackRangeSq = 12f * 12f;  // 거리 12 이하면 공격
    private const int AttackCooldownMs = 500;

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
        if (ps is null || ps.IsDead) return Task.CompletedTask;

        ps.X = request.X;
        ps.Y = request.Y;

        var zone = _zones.GetOrCreate(ps.ZoneId);

        // 이동 브로드캐스트
        var movePacket = PacketHelper.Build(PacketType.MoveResponse,
            new MoveResponse { PlayerId = ps.PlayerId, X = request.X, Y = request.Y, Z = request.Z });
        zone.Broadcast(movePacket, excludePlayerId: ps.PlayerId);
        session.Send(movePacket);

        // 전투 체크
        if ((DateTime.UtcNow - ps.LastAttackTime).TotalMilliseconds >= AttackCooldownMs)
        {
            foreach (var other in zone.GetAllPlayers())
            {
                if (other.PlayerId == ps.PlayerId) continue;
                if (other.IsDead) continue;
                if (other.IsInvincible) continue;

                float dx = ps.X - other.X;
                float dy = ps.Y - other.Y;

                if (dx * dx + dy * dy < AttackRangeSq)
                {
                    ps.LastAttackTime = DateTime.UtcNow;
                    ApplyDamage(ps, other, zone);
                    break;  // 한 번에 한 명만 공격
                }
            }
        }

        return Task.CompletedTask;
    }

    private static void ApplyDamage(PlayerSession attacker, PlayerSession target, Zone zone)
    {
        target.Hp -= attacker.Atk;

        // 피해 브로드캐스트
        zone.Broadcast(PacketType.DamageResponse, new DamageResponse
        {
            AttackerId = attacker.PlayerId,
            TargetId = target.PlayerId,
            Damage = attacker.Atk,
            RemainHp = Math.Max(0, target.Hp),
        });

        // 사망 처리
        if (target.IsDead)
        {
            zone.Broadcast(PacketType.DeathResponse, new DeathResponse
            {
                DeadPlayerId = target.PlayerId,
            });

            // 3초 후 리스폰
            _ = RespawnAsync(target, zone);
        }
    }

    private static async Task RespawnAsync(PlayerSession ps, Zone zone)
    {
        await Task.Delay(3000);

        ps.Hp = ps.MaxHp;
        ps.Atk = 10;
        ps.ColorIndex = 0;
        ps.IsInvincible = true;

        zone.Broadcast(PacketType.RespawnResponse, new RespawnResponse
        {
            PlayerId = ps.PlayerId,
            X = ps.X,
            Y = ps.Y,
            Hp = ps.Hp,
        });

        await Task.Delay(3000);
        ps.IsInvincible = false;
    }
}
