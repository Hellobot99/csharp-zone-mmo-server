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
    public int Hp { get; set; } = 100;
    public int MaxHp { get; set; } = 100;
    public int Atk { get; set; } = 10;
    public bool IsDead => Hp <= 0;
    public DateTime LastAttackTime { get; set; } = DateTime.MinValue;
    public DateTime LastSkillTime { get; set; } = DateTime.MinValue;
    public int ColorIndex { get; set; } = 0;
}
