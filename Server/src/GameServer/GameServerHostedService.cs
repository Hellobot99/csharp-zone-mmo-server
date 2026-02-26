using GameServer.Network;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GameServer;

public sealed class GameServerHostedService : IHostedService
{
    private readonly TcpServer _tcpServer;
    private readonly ILogger<GameServerHostedService> _logger;

    public GameServerHostedService(TcpServer tcpServer, ILogger<GameServerHostedService> logger)
    {
        _tcpServer = tcpServer;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting GameServerHostedService...");
        _tcpServer.Start();
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Stopping GameServerHostedService...");
        _tcpServer.Stop();
        return Task.CompletedTask;
    }
}
