using GameServer.Game;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer;

public sealed class HeartbeatService : BackgroundService
{
    private readonly SessionManager _sessions;
    private readonly ILogger<HeartbeatService> _logger;

    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(90);

    public HeartbeatService(SessionManager sessions, ILogger<HeartbeatService> logger)
    {
        _sessions = sessions;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(CheckInterval, stoppingToken);

            var now = DateTime.UtcNow;
            foreach (var ps in _sessions.GetAll())
            {
                if (ps.IsObserver) continue;
                if ((now - ps.LastPingAt) > Timeout)
                {
                    _logger.LogInformation(
                        "Heartbeat timeout – closing session for player {PlayerId} ({Username})",
                        ps.PlayerId, ps.Username);
                    ps.Connection.Close();
                }
            }
        }
    }
}
