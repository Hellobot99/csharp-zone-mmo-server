using GameProto;
using GameServer.Game;
using GameServer.Network;
using Microsoft.Extensions.Logging;

namespace GameServer.Packets.Handlers;

public sealed class ChatPacketHandler : IPacketHandler
{
    private readonly SessionManager _sessions;
    private readonly ZoneManager _zones;
    private readonly ILogger<ChatPacketHandler> _logger;

    public ChatPacketHandler(SessionManager sessions, ZoneManager zones, ILogger<ChatPacketHandler> logger)
    {
        _sessions = sessions;
        _zones = zones;
        _logger = logger;
    }

    public Task HandleAsync(IClientSession session, byte[] body)
    {
        ChatRequest request;
        try { request = ChatRequest.Parser.ParseFrom(body); }
        catch { return Task.CompletedTask; }

        var ps = _sessions.Get(session.SessionId);
        if (ps is null)
        {
            _logger.LogWarning("[session={Id}] Chat received but not logged in", session.SessionId);
            return Task.CompletedTask;
        }

        _logger.LogInformation("[session={Id}] Chat from '{User}' in zone={Zone}: {Msg}",
            session.SessionId, ps.Username, ps.ZoneId, request.Message);

        var zone = _zones.GetOrCreate(ps.ZoneId);
        zone.Broadcast(PacketType.ChatBroadcast,
            new ChatBroadcast { PlayerId = ps.PlayerId, Username = ps.Username, Message = request.Message });

        return Task.CompletedTask;
    }
}
