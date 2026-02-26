using GameServer.Database.Models;

namespace GameServer.Database.Repositories;

public interface IPlayerRepository
{
    Task<Player?> GetByIdAsync(int id);
    Task<Player?> GetByUsernameAsync(string username);
    Task<Player> CreateAsync(Player player);
    Task UpdateAsync(Player player);
}
