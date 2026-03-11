namespace GameServer.Config;

public class JwtConfig
{
    public const string Section = "Jwt";

    public string Secret { get; set; } = string.Empty;
    public int ExpiryHours { get; set; } = 24;
}
