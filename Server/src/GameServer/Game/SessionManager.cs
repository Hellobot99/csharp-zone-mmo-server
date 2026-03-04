using System.Collections.Concurrent;
using GameServer.Network;

namespace GameServer.Game;

public class SessionManager
{
    private readonly ConcurrentDictionary<int, PlayerSession> _sessions = new();

    public PlayerSession Add(IClientSession connection, int playerId, string username)
    {
        var ps = new PlayerSession
        {
            Connection = connection,
            PlayerId = playerId,
            Username = username,
            ZoneId = 0
        };
        _sessions[connection.SessionId] = ps;
        return ps;
    }

    public void Remove(int sessionId) => _sessions.TryRemove(sessionId, out _);

    public PlayerSession? Get(int sessionId)
        => _sessions.TryGetValue(sessionId, out var ps) ? ps : null;

    public IEnumerable<PlayerSession> GetAll() => _sessions.Values;
}