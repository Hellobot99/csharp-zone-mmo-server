using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using GameServer.Config;
using GameServer.Database.Models;
using GameServer.Database.Repositories;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace GameServer.Api.Services;

public sealed class AuthService : IAuthService
{
    private readonly IPlayerRepository _players;
    private readonly JwtConfig _jwt;

    public AuthService(IPlayerRepository players, IOptions<JwtConfig> jwt)
    {
        _players = players;
        _jwt = jwt.Value;
    }

    public async Task<(bool Success, int PlayerId, string Username, string? Error)> RegisterAsync(string username, string password)
    {
        var existing = await _players.GetByUsernameAsync(username);
        if (existing is not null)
            return (false, 0, string.Empty, "Username already taken");

        var hashed = BCrypt.Net.BCrypt.HashPassword(password);
        var player = await _players.CreateAsync(new Player { Username = username, Password = hashed });
        return (true, player.Id, player.Username, null);
    }

    public async Task<(bool Success, string? Token, int PlayerId, string Username, string? Error)> LoginAsync(string username, string password)
    {
        var player = await _players.GetByUsernameAsync(username);
        if (player is null || !BCrypt.Net.BCrypt.Verify(password, player.Password))
            return (false, null, 0, string.Empty, "Invalid credentials");

        player.LastLoginAt = DateTime.UtcNow;
        await _players.UpdateAsync(player);

        var token = GenerateJwt(player.Id, player.Username);
        return (true, token, player.Id, player.Username, null);
    }

    private string GenerateJwt(int playerId, string username)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, playerId.ToString()),
            new Claim(JwtRegisteredClaimNames.UniqueName, username),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(_jwt.ExpiryHours),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
