using GameServer.Api.Dtos;
using GameServer.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace GameServer.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly IAuthService _auth;

    public AuthController(IAuthService auth) => _auth = auth;

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request)
    {
        var (success, playerId, username, error) = await _auth.RegisterAsync(request.Username, request.Password);
        if (!success) return Conflict(new { error });
        return Ok(new RegisterResponse(playerId, username));
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request)
    {
        var (success, token, playerId, username, error) = await _auth.LoginAsync(request.Username, request.Password);
        if (!success) return Unauthorized(new { error });
        return Ok(new LoginResponse(token!, playerId, username));
    }
}
