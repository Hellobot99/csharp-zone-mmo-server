using GameServer.Network;

namespace GameServer.Game;

public class PlayerSession
{
    public required IClientSession Connection { get; set; }
    public int PlayerId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int ZoneId { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public bool IsObserver { get; set; }
    public DateTime LastPingAt { get; set; } = DateTime.UtcNow;
}
