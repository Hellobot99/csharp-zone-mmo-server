using StackExchange.Redis;

namespace GameServer.Cache;

public sealed class RedisService : IRedisService
{
    private readonly IDatabase _db;

    public RedisService(IConnectionMultiplexer redis)
    {
        _db = redis.GetDatabase();
    }

    // ── Generic KV ────────────────────────────────────────────────────────────

    public async Task SetAsync(string key, string value, TimeSpan? expiry = null) =>
        await _db.StringSetAsync(key, value, expiry);

    public async Task<string?> GetAsync(string key)
    {
        var value = await _db.StringGetAsync(key);
        return value.HasValue ? (string?)value : null;
    }

    public Task<bool> DeleteAsync(string key) => _db.KeyDeleteAsync(key);

    public Task<bool> ExistsAsync(string key) => _db.KeyExistsAsync(key);

    // ── Session helpers ───────────────────────────────────────────────────────

    public Task SetSessionAsync(int playerId, string token, TimeSpan expiry) =>
        _db.StringSetAsync(SessionKey(token), playerId.ToString(), expiry);

    public async Task<int?> GetPlayerIdByTokenAsync(string token)
    {
        var value = await _db.StringGetAsync(SessionKey(token));
        return value.HasValue && int.TryParse(value, out int id) ? id : null;
    }

    public Task RemoveSessionAsync(string token) =>
        _db.KeyDeleteAsync(SessionKey(token));

    // ── Key helpers ───────────────────────────────────────────────────────────

    private static string SessionKey(string token) => $"session:{token}";
}
