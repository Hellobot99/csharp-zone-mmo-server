using System.Collections.Concurrent;
using Google.Protobuf;
using GameServer.Packets;

namespace GameServer.Game;

public class Zone
{
    public int ZoneId { get; set; }
    ConcurrentDictionary<int, PlayerSession> _players = new();
    internal ConcurrentDictionary<int, PlayerSession>? Observers { get; set; }

    public IReadOnlyCollection<PlayerSession> GetAllPlayers() { return _players.Values.ToArray(); }

    public void Enter(PlayerSession ps)
    {
        ps.ZoneId = ZoneId;
        _players[ps.PlayerId] = ps;
    }

    public void Leave(int playerId)
    {
        _players.TryRemove(playerId, out _);
    }

    public void Broadcast(PacketType type, IMessage message, int? excludePlayerId = null)
        => Broadcast(PacketHelper.Build(type, message), excludePlayerId);

    public void Broadcast(byte[] packet, int? excludePlayerId = null)
    {
        foreach (var (id, ps) in _players)
        {
            if (id == excludePlayerId) continue;
            ps.Connection.Send(packet);
        }
        if (Observers != null)
            foreach (var (id, obs) in Observers)
            {
                if (id == excludePlayerId) continue;
                obs.Connection.Send(packet);
            }
    }
}