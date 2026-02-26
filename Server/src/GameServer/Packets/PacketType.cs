namespace GameServer.Packets;

/// <summary>
/// Canonical packet type IDs shared between client and server.
/// Odd numbers = client → server (requests).
/// Even numbers = server → client (responses / pushes).
/// </summary>
public enum PacketType : ushort
{
    // ── Auth ──────────────────────────────────────────────────────────────────
    LoginRequest  = 0x0001,
    LoginResponse = 0x0002,

    // ── Heartbeat ─────────────────────────────────────────────────────────────
    Ping = 0x0011,
    Pong = 0x0012,

    // ── Game (placeholder) ────────────────────────────────────────────────────
    MoveRequest   = 0x0101,
    MoveResponse  = 0x0102,
    ChatRequest   = 0x0201,
    ChatBroadcast = 0x0202,
}
