namespace GameServer.Packets;

/// <summary>
/// Canonical packet type IDs shared between client and server.
/// Odd numbers = client → server (requests).
/// Even numbers = server → client (responses / pushes).
/// </summary>
public enum PacketType : ushort
{
    // ── Auth ──────────────────────────────────────────────────────────────────
    LoginRequest = 0x0001,
    LoginResponse = 0x0002,
    RegisterRequest = 0x0003,
    RegisterResponse = 0x0004,

    // ── Heartbeat ─────────────────────────────────────────────────────────────
    Ping = 0x0011,
    Pong = 0x0012,

    // ── Game (placeholder) ────────────────────────────────────────────────────
    MoveRequest = 0x0101,
    MoveResponse = 0x0102,
    RequestSnapshot = 0x0103,
    ZoneSnapshot = 0x0104,
    PlayerEnter = 0x0105,
    PlayerLeave = 0x0106,
    ZoneTransferRequest = 0x0107,
    ZoneTransferResponse = 0x0108,
    EnterGame = 0x0109,
    ChatRequest = 0x0201,
    ChatBroadcast = 0x0202,
}
