using GameServer.Database.Models;
using Microsoft.EntityFrameworkCore;

namespace GameServer.Database;

public class GameDbContext : DbContext
{
    public GameDbContext(DbContextOptions<GameDbContext> options) : base(options) { }

    public DbSet<Player> Players => Set<Player>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Player>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.HasIndex(e => e.Username).IsUnique();

            entity.Property(e => e.Username)
                .HasMaxLength(50)
                .IsRequired();

            entity.Property(e => e.PasswordHash)
                .HasMaxLength(255)
                .IsRequired();

            entity.Property(e => e.Level)
                .HasDefaultValue(1);

            entity.Property(e => e.Experience)
                .HasDefaultValue(0L);

            entity.Property(e => e.CreatedAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");

            entity.Property(e => e.LastLoginAt)
                .HasDefaultValueSql("CURRENT_TIMESTAMP(6)");
        });
    }
}
