public static class PacketType
{
    // TCP Auth
    // Login and Register are handled by the HTTP API.
    // After receiving a JWT, the client connects via TCP and sends TcpAuthRequest.
    public const ushort TcpAuthRequest  = 0x0001;
    public const ushort TcpAuthResponse = 0x0002;

    // Heartbeat
    public const ushort Ping = 0x0011;
    public const ushort Pong = 0x0012;

    // Game
    public const ushort MoveRequest         = 0x0101;
    public const ushort MoveResponse        = 0x0102;
    public const ushort RequestSnapshot     = 0x0103;
    public const ushort ZoneSnapshot        = 0x0104;
    public const ushort PlayerEnter         = 0x0105;
    public const ushort PlayerLeave         = 0x0106;
    public const ushort ZoneTransferRequest = 0x0107;
    public const ushort ZoneTransferResponse= 0x0108;
    public const ushort EnterGame           = 0x0109;
    public const ushort ChatRequest         = 0x0201;
    public const ushort ChatBroadcast       = 0x0202;
}
