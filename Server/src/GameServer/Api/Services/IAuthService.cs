namespace GameServer.Api.Services;

public interface IAuthService
{
    Task<(bool Success, int PlayerId, string Username, string? Error)> RegisterAsync(string username, string password);
    Task<(bool Success, string? Token, int PlayerId, string Username, string? Error)> LoginAsync(string username, string password);
}
