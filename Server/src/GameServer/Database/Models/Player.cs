namespace GameServer.Database.Models;

public class Player
{
    public int Id { get; set; }

    /// <summary>Unique login name (max 50 chars).</summary>
    public string Username { get; set; } = string.Empty;

    public string Password { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}
