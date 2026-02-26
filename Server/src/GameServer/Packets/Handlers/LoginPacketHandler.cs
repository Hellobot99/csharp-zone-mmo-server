using Google.Protobuf;
using GameProto;
using GameServer.Cache;

using GameServer.Database.Repositories;
using GameServer.Network;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

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
        LoginRequest request;
        try
        {
            request = LoginRequest.Parser.ParseFrom(body);
        }
        catch
        {
            SendResponse(session, new LoginResponse { Success = false, Token = "Malformed packet" });
            return;
        }

        var player = await _players.GetByUsernameAsync(request.Username);

        if (player is null || player.Password != request.Password)
        {
            _logger.LogWarning("[session={Id}] Failed login attempt for '{User}'", session.SessionId, request.Username);
            SendResponse(session, new LoginResponse { Success = false, Token = "Invalid credentials" });
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        await _redis.SetSessionAsync(player.Id, token, TimeSpan.FromHours(24));

        player.LastLoginAt = DateTime.UtcNow;
        await _players.UpdateAsync(player);

        _logger.LogInformation("[session={Id}] Player '{User}' (id={PlayerId}) logged in.",
            session.SessionId, request.Username, player.Id);

        SendResponse(session, new LoginResponse { Success = true, PlayerId = player.Id, Token = token });
    }

    private static void SendResponse(IClientSession session, LoginResponse response)
    {
        var body = response.ToByteArray();
        var packet = new byte[4 + body.Length];
        int pos = 0;

        BitConverter.TryWriteBytes(packet.AsSpan(pos), (ushort)(4 + body.Length)); pos += 2;
        BitConverter.TryWriteBytes(packet.AsSpan(pos), (ushort)PacketType.LoginResponse); pos += 2;
        body.CopyTo(packet, pos);

        session.Send(packet);
    }
}
