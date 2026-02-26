using System.Text;
using GameServer.Cache;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using GameServer.Network;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

/// <summary>
/// Handles LoginRequest (0x0001) packets and replies with a LoginResponse (0x0002).
///
/// Request body layout (all strings are UTF-8):
///   [usernameLen : byte][username : usernameLen bytes]
///   [passwordLen : byte][password : passwordLen bytes]   ← SHA-256 hex hash recommended
///
/// Response body layout:
///   [success    : byte]          1 = OK, 0 = fail
///   [playerId   : int32 LE]      0 on failure
///   [tokenLen   : byte]
///   [token      : tokenLen bytes]   session token on success, error message on failure
/// </summary>
public sealed class LoginPacketHandler : IPacketHandler
{
    private readonly IPlayerRepository _players;
    private readonly IRedisService _redis;
    private readonly ILogger<LoginPacketHandler> _logger;

    public LoginPacketHandler(
        IPlayerRepository players,
        IRedisService redis,
        ILogger<LoginPacketHandler> logger)
    {
        _players = players;
        _redis = redis;
        _logger = logger;
    }

    public async Task HandleAsync(IClientSession session, byte[] body)
    {
        // ── Parse request ─────────────────────────────────────────────────────
        int offset = 0;

        if (body.Length < 2)
        {
            SendResponse(session, false, 0, "Malformed packet");
            return;
        }

        byte usernameLen = body[offset++];
        if (offset + usernameLen > body.Length) { SendResponse(session, false, 0, "Malformed packet"); return; }
        string username = Encoding.UTF8.GetString(body, offset, usernameLen);
        offset += usernameLen;

        byte passwordLen = body[offset++];
        if (offset + passwordLen > body.Length) { SendResponse(session, false, 0, "Malformed packet"); return; }
        string password = Encoding.UTF8.GetString(body, offset, passwordLen);

        // ── Authenticate ──────────────────────────────────────────────────────
        var player = await _players.GetByUsernameAsync(username);

        // NOTE: In production use BCrypt / Argon2 for password verification.
        // Here we compare the raw value supplied by the client (expected to be pre-hashed).
        if (player is null || player.PasswordHash != password)
        {
            _logger.LogWarning("[session={Id}] Failed login attempt for '{User}'", session.SessionId, username);
            SendResponse(session, false, 0, "Invalid credentials");
            return;
        }

        // ── Issue session token ───────────────────────────────────────────────
        var token = Guid.NewGuid().ToString("N");
        await _redis.SetSessionAsync(player.Id, token, TimeSpan.FromHours(24));

        player.LastLoginAt = DateTime.UtcNow;
        await _players.UpdateAsync(player);

        _logger.LogInformation("[session={Id}] Player '{User}' (id={PlayerId}) logged in.",
            session.SessionId, username, player.Id);

        SendResponse(session, true, player.Id, token);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static void SendResponse(IClientSession session, bool success, int playerId, string payload)
    {
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        // Body: [success:1][playerId:4][payloadLen:1][payload:n]
        int bodyLen = 1 + 4 + 1 + payloadBytes.Length;
        int totalLen = 4 + bodyLen; // header + body

        var packet = new byte[totalLen];
        int pos = 0;

        // Header
        BitConverter.TryWriteBytes(packet.AsSpan(pos), (ushort)totalLen); pos += 2;
        BitConverter.TryWriteBytes(packet.AsSpan(pos), (ushort)PacketType.LoginResponse); pos += 2;

        // Body
        packet[pos++] = success ? (byte)1 : (byte)0;
        BitConverter.TryWriteBytes(packet.AsSpan(pos), playerId); pos += 4;
        packet[pos++] = (byte)payloadBytes.Length;
        payloadBytes.CopyTo(packet, pos);

        session.Send(packet);
    }
}
