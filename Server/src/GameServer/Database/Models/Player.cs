namespace GameServer.Database.Models;

public class Player
{
    public int Id { get; set; }

    /// <summary>Unique login name (max 50 chars).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Hashed password. Use BCrypt/Argon2 in production.</summary>
    public string PasswordHash { get; set; } = string.Empty;

    public int Level { get; set; } = 1;
    public long Experience { get; set; } = 0;

    public DateTime CreatedAt { get; set; }
    public DateTime LastLoginAt { get; set; }
}
