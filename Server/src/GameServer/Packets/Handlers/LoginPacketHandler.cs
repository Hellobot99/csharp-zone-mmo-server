using GameProto;
using GameServer.Cache;
using GameServer.Database.Repositories;
using GameServer.Game;
using GameServer.Network;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

public sealed class LoginPacketHandler : IPacketHandler
{
    private readonly IPlayerRepository _players;
    private readonly IRedisService _redis;
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;
    private readonly ILogger<LoginPacketHandler> _logger;

    public LoginPacketHandler(
        IPlayerRepository players,
        IRedisService redis,
        SessionManager sessions,
        ZoneManager zones,
        ILogger<LoginPacketHandler> logger)
    {
        _players = players;
        _redis = redis;
        _sessions = sessions;
        _zones = zones;
        _logger = logger;
    }

    public async Task HandleAsync(IClientSession session, byte[] body)
    {
        LoginRequest request;
        try { request = LoginRequest.Parser.ParseFrom(body); }
        catch
        {
            session.Send(PacketType.LoginResponse, new LoginResponse { Success = false, Token = "Malformed packet" });
            return;
        }

        bool isObserver = request.Username.StartsWith("~");
        var lookupUsername = isObserver ? request.Username[1..] : request.Username;

        var player = await _players.GetByUsernameAsync(lookupUsername);
        if (player is null || player.Password != request.Password)
        {
            _logger.LogWarning("[session={Id}] Failed login for '{User}'", session.SessionId, lookupUsername);
            session.Send(PacketType.LoginResponse, new LoginResponse { Success = false, Token = "Invalid credentials" });
            return;
        }

        var token = Guid.NewGuid().ToString("N");
        await _redis.SetSessionAsync(player.Id, token, TimeSpan.FromHours(24));
        player.LastLoginAt = DateTime.UtcNow;
        await _players.UpdateAsync(player);

        // 1. 존 입장 (Zone1 스폰: 0, 600)
        var zone = _zones.GetOrCreate(1);
        var ps = _sessions.Add(session, player.Id, player.Username);
        ps.IsObserver = isObserver;
        ps.X = 0f;
        ps.Y = 600f;
        _zones.Enter(ps, zoneId: 1);

        _logger.LogInformation("[session={Id}] Player '{User}' (id={PlayerId}) logged in.", session.SessionId, player.Username, player.Id);

        // 2. LoginResponse 전송 (클라이언트가 TownScene 로드 후 EnterGame 패킷을 전송)
        session.Send(PacketType.LoginResponse, new LoginResponse { Success = true, PlayerId = player.Id, Token = token });
    }
}
