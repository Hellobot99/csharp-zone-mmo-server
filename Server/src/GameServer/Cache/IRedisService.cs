namespace GameServer.Cache;

public interface IRedisService
{
    // ── Generic KV ────────────────────────────────────────────────────────────
    Task SetAsync(string key, string value, TimeSpan? expiry = null);
    Task<string?> GetAsync(string key);
    Task<bool> DeleteAsync(string key);
    Task<bool> ExistsAsync(string key);

    // ── Session helpers ───────────────────────────────────────────────────────
    Task SetSessionAsync(int playerId, string token, TimeSpan expiry);
    Task<int?> GetPlayerIdByTokenAsync(string token);
    Task RemoveSessionAsync(string token);
}
