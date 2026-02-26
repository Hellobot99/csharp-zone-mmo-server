using System.Collections.Concurrent;
using GameServer.Network;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

/// <summary>
/// Registry that maps packet type IDs to handler types and dispatches inbound packets.
///
/// A new DI scope is created for every packet dispatch so that handlers can safely
/// consume scoped services (e.g. DbContext, repositories).
/// </summary>
public sealed class PacketHandlerManager
{
    private readonly IServiceProvider _services;
    private readonly ILogger<PacketHandlerManager> _logger;
    private readonly ConcurrentDictionary<ushort, Type> _registry = new();

    public PacketHandlerManager(IServiceProvider services, ILogger<PacketHandlerManager> logger)
    {
        _services = services;
        _logger = logger;
    }

    /// <summary>Registers a handler type for the given packet type ID.</summary>
    public void Register<THandler>(ushort packetType) where THandler : IPacketHandler
    {
        _registry[packetType] = typeof(THandler);
        _logger.LogDebug("Registered handler {Handler} for packet 0x{Type:X4}",
            typeof(THandler).Name, packetType);
    }

    /// <summary>
    /// Resolves and invokes the handler registered for <paramref name="packetType"/>.
    /// Matches the <c>Func&lt;IClientSession, ushort, byte[], Task&gt;</c> delegate shape
    /// so it can be subscribed directly to <see cref="Network.TcpSession.PacketReceived"/>.
    /// </summary>
    public async Task DispatchAsync(IClientSession session, ushort packetType, byte[] body)
    {
        if (!_registry.TryGetValue(packetType, out var handlerType))
        {
            _logger.LogWarning("[session={Id}] No handler for packet type 0x{Type:X4} – ignoring.",
                session.SessionId, packetType);
            return;
        }

        using var scope = _services.CreateScope();
        var handler = (IPacketHandler)scope.ServiceProvider.GetRequiredService(handlerType);
        await handler.HandleAsync(session, body);
    }
}
