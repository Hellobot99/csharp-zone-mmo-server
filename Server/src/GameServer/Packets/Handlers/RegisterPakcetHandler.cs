using GameProto;
using Google.Protobuf;
using GameServer.Cache;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using GameServer.Network;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

public sealed class RegisterPacketHandler : IPacketHandler
{
    private readonly IPlayerRepository _players;
    private readonly IRedisService _redis;
    private readonly ILogger<RegisterPacketHandler> _logger;

    public RegisterPacketHandler(IPlayerRepository players, IRedisService redis, ILogger<RegisterPacketHandler> logger)
    {
        _players = players;
        _redis = redis;
        _logger = logger;
    }

    public async Task HandleAsync(IClientSession session, byte[] body)
    {
        RegisterRequest request;
        try { request = RegisterRequest.Parser.ParseFrom(body); }
        catch
        {
            session.Send(PacketType.RegisterResponse, new RegisterResponse { Success = false, Token = "Malformed packet" });
            return;
        }

        var existing = await _players.GetByUsernameAsync(request.Username);
        if (existing is not null)
        {
            _logger.LogWarning("[session={Id}] Register failed - '{User}' already exists", session.SessionId, request.Username);
            session.Send(PacketType.RegisterResponse, new RegisterResponse { Success = false, Token = "Username already taken" });
            return;
        }

        var player = await _players.CreateAsync(new Player { Username = request.Username, Password = request.Password });

        _logger.LogInformation("[session={Id}] Player '{User}' (id={PlayerId}) registered.", session.SessionId, player.Username, player.Id);
        session.Send(PacketType.RegisterResponse, new RegisterResponse { Success = true, PlayerId = player.Id, Token = "" });
    }
}
