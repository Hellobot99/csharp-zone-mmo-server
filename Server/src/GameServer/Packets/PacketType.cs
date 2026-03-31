namespace GameServer.Packets;

/// <summary>
/// Canonical packet type IDs shared between client and server.
/// Odd numbers = client → server (requests).
/// Even numbers = server → client (responses / pushes).
/// </summary>
public enum PacketType : ushort
{
    // ── Auth (TCP) ────────────────────────────────────────────────────────────
    // Login/Register are now handled by HTTP API (POST /api/auth/login, /register).
    // After receiving a JWT from the API, the client connects via TCP and sends TcpAuthRequest.
    TcpAuthRequest = 0x0001,
    TcpAuthResponse = 0x0002,

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

    // ── Combat ────────────────────────────────────────────────────────────────
    SkillRequest = 0x0301,
    SkillResponse = 0x0302,
    DamageResponse = 0x0304,
    DeathResponse = 0x0306,
    RespawnResponse = 0x0308,

    // ── Matchmaking ───────────────────────────────────────────────────────────
    MatchmakeRequest  = 0x0401,
    MatchmakeResponse = 0x0402,
    MatchStarted      = 0x0404,
    MatchEnded        = 0x0406,

}
