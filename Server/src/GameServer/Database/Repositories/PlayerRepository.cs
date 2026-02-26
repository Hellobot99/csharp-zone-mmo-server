using GameServer.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Database.Repositories;

public sealed class PlayerRepository : IPlayerRepository
{
    private readonly GameDbContext _db;

    public PlayerRepository(GameDbContext db) => _db = db;

    public Task<Player?> GetByIdAsync(int id) =>
        _db.Players.FindAsync(id).AsTask();

    public Task<Player?> GetByUsernameAsync(string username) =>
        _db.Players.FirstOrDefaultAsync(p => p.Username == username);

    public async Task<Player> CreateAsync(Player player)
    {
        _db.Players.Add(player);
        await _db.SaveChangesAsync();
        return player;
    }

    public async Task UpdateAsync(Player player)
    {
        _db.Players.Update(player);
        await _db.SaveChangesAsync();
    }
}
