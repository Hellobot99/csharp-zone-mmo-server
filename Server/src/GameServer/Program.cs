using System.Text;
using GameServer;
using GameServer.Api.Services;
using GameServer.Cache;
using GameServer.Config;
using GameServer.Database;
using GameServer.Database.Repositories;
using GameServer.Game;
using GameServer.Network;
using GameServer.Packets;
using GameServer.Packets.Handlers;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// ── Configuration ──────────────────────────────────────────────────────────────
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);
builder.Configuration.AddEnvironmentVariables();

// ── Server Config ──────────────────────────────────────────────────────────────
builder.Services.Configure<ServerConfig>(builder.Configuration.GetSection(ServerConfig.Section));
builder.Services.Configure<JwtConfig>(builder.Configuration.GetSection(JwtConfig.Section));

// ── Database (MySQL via EF Core) ───────────────────────────────────────────────
var mysqlConnStr = builder.Configuration.GetConnectionString("MySqlConnection")
    ?? throw new InvalidOperationException("MySqlConnection not configured.");
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

// ── Auth Service ───────────────────────────────────────────────────────────────
builder.Services.AddScoped<IAuthService, AuthService>();

// ── Game ───────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<ZoneManager>();

// ── Packet Handlers ────────────────────────────────────────────────────────────
builder.Services.AddScoped<TcpAuthHandler>();
builder.Services.AddScoped<MovePacketHandler>();
builder.Services.AddScoped<ChatPacketHandler>();
builder.Services.AddScoped<RequestSnapshotHandler>();
builder.Services.AddScoped<ZoneTransferHandler>();
builder.Services.AddScoped<EnterGameHandler>();
builder.Services.AddScoped<PingPacketHandler>();
builder.Services.AddScoped<SkillPacketHandler>();


builder.Services.AddSingleton<PacketHandlerManager>(sp =>
{
    var manager = new PacketHandlerManager(sp, sp.GetRequiredService<ILogger<PacketHandlerManager>>());
    manager.Register<TcpAuthHandler>((ushort)PacketType.TcpAuthRequest);
    manager.Register<MovePacketHandler>((ushort)PacketType.MoveRequest);
    manager.Register<ChatPacketHandler>((ushort)PacketType.ChatRequest);
    manager.Register<RequestSnapshotHandler>((ushort)PacketType.RequestSnapshot);
    manager.Register<ZoneTransferHandler>((ushort)PacketType.ZoneTransferRequest);
    manager.Register<EnterGameHandler>((ushort)PacketType.EnterGame);
    manager.Register<PingPacketHandler>((ushort)PacketType.Ping);
    manager.Register<SkillPacketHandler>((ushort)PacketType.SkillRequest);

    return manager;
});

// ── Network ────────────────────────────────────────────────────────────────────
builder.Services.AddSingleton<TcpServer>();
builder.Services.AddHostedService<GameServerHostedService>();
builder.Services.AddHostedService<HeartbeatService>();
builder.Services.AddSingleton<SkillPacketHandler>();


// ── JWT Authentication ─────────────────────────────────────────────────────────
var jwtSecret = builder.Configuration["Jwt:Secret"]
    ?? throw new InvalidOperationException("Jwt:Secret not configured.");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateIssuer = false,
            ValidateAudience = false,
            ClockSkew = TimeSpan.Zero
        };
    });

// ── Controllers ────────────────────────────────────────────────────────────────
builder.Services.AddControllers();

var app = builder.Build();

// ── EF Core schema ─────────────────────────────────────────────────────────────
var startupLogger = app.Services.GetRequiredService<ILogger<Program>>();
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<GameDbContext>();
    for (int attempts = 5; attempts > 0; attempts--)
    {
        try
        {
            // EnsureCreated returns false when the DB already exists (e.g. created by Docker init).
            // In that case tables might be missing, so verify and recreate if needed.
            bool created = await db.Database.EnsureCreatedAsync();
            if (!created)
            {
                try { await db.Players.AnyAsync(); }
                catch
                {
                    startupLogger.LogWarning("DB exists but schema is missing. Recreating schema...");
                    await db.Database.EnsureDeletedAsync();
                    await db.Database.EnsureCreatedAsync();
                }
            }
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

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async ctx =>
    {
        var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
        var log = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        log.LogError(ex, "Unhandled exception in HTTP request");
        ctx.Response.StatusCode = 500;
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsync($"{{\"error\":\"{ex?.Message}\"}}");
    });
});
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

await app.RunAsync();
