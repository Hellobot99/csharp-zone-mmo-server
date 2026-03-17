using GameProto;
using GameServer.Game;
using GameServer.Network;
using GameServer.Packets;

namespace GameServer.Packets.Handlers;

public sealed class SkillPacketHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;

    private const int CooldownMs = 3000;  // 스킬 쿨다운 3초

    public SkillPacketHandler(SessionManager sessions, ZoneManager zones)
    {
        _sessions = sessions;
        _zones = zones;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var ps = _sessions.Get(session.SessionId);
        if (ps is null || ps.IsDead) return Task.CompletedTask;

        // 쿨다운 체크
        if ((DateTime.UtcNow - ps.LastAttackTime).TotalMilliseconds < CooldownMs)
            return Task.CompletedTask;

        ps.LastAttackTime = DateTime.UtcNow;
        ps.Atk += 1;
        ps.ColorIndex += 1;

        var zone = _zones.GetOrCreate(ps.ZoneId);
        zone.Broadcast(PacketType.SkillResponse, new SkillResponse
        {
            PlayerId = ps.PlayerId,
            ColorIndex = ps.ColorIndex,
            Atk = ps.Atk,
        });

        return Task.CompletedTask;
    }
}
