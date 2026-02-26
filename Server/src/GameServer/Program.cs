using GameServer;
using GameServer.Cache;
using GameServer.Config;
using GameServer.Database;
using GameServer.Database.Repositories;
using GameServer.Network;
using GameServer.Packets;
using GameServer.Packets.Handlers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using StackExchange.Redis;

var builder = Host.CreateApplicationBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables(); // env vars override appsettings (Docker / ACI)

// ── Logging ────────────────────────────────────────────────────────────────────
builder.Logging.ClearProviders();
builder.Logging.AddConsole();

// ── Server Config ──────────────────────────────────────────────────────────────
builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection(ServerConfig.Section));

// ── Database (MySQL via EF Core) ───────────────────────────────────────────────
var mysqlConnStr = builder.Configuration.GetConnectionString("MySqlConnection")
    ?? throw new InvalidOperationException("MySqlConnection not configured.");
// MySQL 8.0 고정 버전 사용 (AutoDetect는 빌드/시작 시 실제 DB 접속을 시도해서 불안정)
builder.Services.AddDbContext<GameDbContext>(options =>
    options.UseMySql(mysqlConnStr, new MySqlServerVersion(new Version(8, 0, 36))));

// ── Redis ──────────────────────────────────────────────────────────────────────
var redisConnStr = builder.Configuration.GetConnectionString("Redis")
    ?? throw new InvalidOperationException("Redis connection string not configured.");
builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(redisConnStr));
builder.Services.AddSingleton<IRedisService, RedisService>();

// ── Repositories ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IPlayerRepository, PlayerRepository>();

// ── Packet Handlers ────────────────────────────────────────────────────────────
builder.Services.AddScoped<LoginPacketHandler>();

builder.Services.AddSingleton<PacketHandlerManager>(sp =>
{
    var manager = new PacketHandlerManager(sp, sp.GetRequiredService<ILogger<PacketHandlerManager>>());
    manager.Register<LoginPacketHandler>((ushort)PacketType.LoginRequest);
    return manager;
});

// ── Network ────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TcpServer>();
builder.Services.AddHostedService<GameServerHostedService>();

// ── Build & Run ────────────────────────────────────────────────────────────────
var host = builder.Build();

// Apply EF Core schema on startup (with retry for Docker readiness)
var startupLogger = host.Services.GetRequiredService<ILogger<Program>>();
using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    for (int attempts = 5; attempts > 0; attempts--)
    {
        try
        {
            // EnsureCreated creates tables from the model without migrations.
            // For production, replace with: await db.Database.MigrateAsync();
            // and run: dotnet ef migrations add InitialCreate
            await db.Database.EnsureCreatedAsync();
            startupLogger.LogInformation("Database schema ready.");
            break;
        }
        catch (Exception ex)
        {
            if (attempts == 1) throw;
            startupLogger.LogWarning(ex, "Database not ready. Retrying in 5 s... ({N} attempts left)", attempts - 1);
            await Task.Delay(TimeSpan.FromSeconds(5));
        }
    }
}

await host.RunAsync();
