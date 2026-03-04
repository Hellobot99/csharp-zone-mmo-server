using System.Collections.Concurrent;

namespace GameServer.Game;

public class ZoneManager
{
    private readonly ConcurrentDictionary<int, Zone> _zones = new();
    private readonly ConcurrentDictionary<int, PlayerSession> _observers = new();

    public Zone GetOrCreate(int zoneId)
        => _zones.GetOrAdd(zoneId, id => new Zone { ZoneId = id, Observers = _observers });

    public void Enter(PlayerSession ps, int zoneId)
    {
        if (ps.ZoneId != 0)
            Leave(ps);

        if (ps.IsObserver)
        {
            ps.ZoneId = zoneId;
            _observers[ps.PlayerId] = ps;
            return;
        }

        var zone = GetOrCreate(zoneId);
        zone.Enter(ps);
    }

    public void Leave(PlayerSession ps)
    {
        if (ps.IsObserver)
        {
            _observers.TryRemove(ps.PlayerId, out _);
            return;
        }
        if (_zones.TryGetValue(ps.ZoneId, out var zone))
            zone.Leave(ps.PlayerId);
    }

    public IEnumerable<PlayerSession> GetAllPlayers()
        => _zones.Values.SelectMany(z => z.GetAllPlayers());
}
