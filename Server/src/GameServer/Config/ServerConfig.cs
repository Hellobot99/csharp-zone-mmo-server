namespace GameServer.Config;

public class ServerConfig
{
    public const string Section = "Server";

    public int Port { get; set; } = 7000;
    public int MaxConnections { get; set; } = 1000;

    /// <summary>
    /// Size of each receive buffer slice in bytes.
    /// Total buffer pool = MaxConnections * BufferSize bytes.
    /// </summary>
    public int BufferSize { get; set; } = 4096;

    public int Backlog { get; set; } = 100;
}
