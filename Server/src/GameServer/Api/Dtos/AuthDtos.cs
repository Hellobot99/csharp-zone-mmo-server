namespace GameServer.Api.Dtos;

public record RegisterRequest(string Username, string Password);
public record RegisterResponse(int PlayerId, string Username);

public record LoginRequest(string Username, string Password);
public record LoginResponse(string Token, int PlayerId, string Username);
