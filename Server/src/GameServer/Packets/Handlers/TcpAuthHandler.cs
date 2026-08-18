using System.IdentityModel.Tokens.Jwt;
using System.Text;
using GameProto;
using GameServer.Config;
using GameServer.Game;
using GameServer.Network;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Packets.Handlers;

/// <summary>
/// Handles TCP authentication after the client obtains a JWT from the HTTP API.
/// Flow: HTTP POST /api/auth/login → JWT → TCP TcpAuthRequest (body = JWT bytes) → game session
/// </summary>
public sealed class TcpAuthHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;
    private readonly JwtConfig _jwt;
    private readonly ILogger<TcpAuthHandler> _logger;

    public TcpAuthHandler(
        SessionManager sessions,
        ZoneManager zones,
        IOptions<JwtConfig> jwt,
        ILogger<TcpAuthHandler> logger)
    {
        _sessions = sessions;
        _zones = zones;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        var token = Encoding.UTF8.GetString(body);

        JwtSecurityToken jwt;
        try
        {
            var handler = new JwtSecurityTokenHandler();
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
            handler.ValidateToken(token, new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            }, out var validated);
            jwt = (JwtSecurityToken)validated;
        }
        catch
        {
            session.Send(PacketType.TcpAuthResponse, new LoginResponse { Success = false, Token = "Invalid token" });
            return Task.CompletedTask;
        }

        var playerId = int.Parse(jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value);
        var username = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.UniqueName).Value;

        var existing = _sessions.GetByPlayerId(playerId);
        if (existing is not null)
        {
            _logger.LogInformation("[session={Id}] Player '{User}' reconnected – closing old session.", session.SessionId, username);
            existing.Connection.Close();
        }

        var ps = _sessions.Add(session, playerId, username);
        ps.IsObserver = username.StartsWith('~');
        ps.X = MatchmakingManager.LobbySpawn.X;
        ps.Y = MatchmakingManager.LobbySpawn.Y;
        _zones.Enter(ps, zoneId: MatchmakingManager.LobbyZoneId);

        _logger.LogInformation("[session={Id}] Player '{User}' (id={PlayerId}) authenticated via JWT.", session.SessionId, username, playerId);
        session.Send(PacketType.TcpAuthResponse, new LoginResponse { Success = true, PlayerId = playerId, Token = token });
        return Task.CompletedTask;
    }
}
