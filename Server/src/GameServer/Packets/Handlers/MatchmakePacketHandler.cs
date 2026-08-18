using GameServer.Game;
using GameServer.Network;

namespace GameServer.Packets.Handlers;

public sealed class MatchmakePacketHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly MatchmakingManager _matchmaking;

    public MatchmakePacketHandler(SessionManager sessions, MatchmakingManager matchmaking)
    {
        _sessions = sessions;
        _matchmaking = matchmaking;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var ps = _sessions.Get(session.SessionId);
        if (ps is null || ps.IsDead) return Task.CompletedTask;

        _matchmaking.Enqueue(ps);
        return Task.CompletedTask;
    }
}
