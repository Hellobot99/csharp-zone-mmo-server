using Google.Protobuf;
using GameServer.Network;

namespace GameServer.Packets;

public static class PacketHelper
{
    /// <summary>단일 세션 전송용 (extension method)</summary>
    public static void Send(this IClientSession session, PacketType type, IMessage message)
        => session.Send(Build(type, message));

    /// <summary>브로드캐스트용 byte[] 빌드</summary>
    public static byte[] Build(PacketType type, IMessage message)
    {
        var body = message.ToByteArray();
        var packet = new byte[4 + body.Length];
        BitConverter.TryWriteBytes(packet.AsSpan(0), (ushort)(4 + body.Length));
        BitConverter.TryWriteBytes(packet.AsSpan(2), (ushort)type);
        body.CopyTo(packet, 4);
        return packet;
    }
}
