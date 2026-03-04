using System.Collections.Concurrent;
using System.Net.Sockets;
using GameProto;
using Google.Protobuf;

namespace TestClient;

public class BotClient
{
    // 3x3 그리드: Zone1 Zone2 Zone3 / Zone4 Zone5 Zone6 / Zone7 Zone8 Zone9
    private static readonly Dictionary<int, (float X1, float X2, float Y1, float Y2)> ZoneBounds = new()
    {
        [1] = (-140f, 140f, 460f, 740f), [2] = (160f, 440f, 460f, 740f), [3] = (460f, 740f, 460f, 740f),
        [4] = (-140f, 140f, 160f, 440f), [5] = (160f, 440f, 160f, 440f), [6] = (460f, 740f, 160f, 440f),
        [7] = (-140f, 140f,-140f, 140f), [8] = (160f, 440f,-140f, 140f), [9] = (460f, 740f,-140f, 140f),
    };
    private static readonly Dictionary<int, (int R, int L, int U, int D)> Neighbors = new()
    {
        [1] = (2, 3, 7, 4), [2] = (3, 1, 8, 5), [3] = (1, 2, 9, 6),
        [4] = (5, 6, 1, 7), [5] = (6, 4, 2, 8), [6] = (4, 5, 3, 9),
        [7] = (8, 9, 4, 1), [8] = (9, 7, 5, 2), [9] = (7, 8, 6, 3),
    };
    private static readonly Dictionary<int, (float X, float Y)> SpawnPoints = new()
    {
        [1] = (  0f, 600f), [2] = (300f, 600f), [3] = (600f, 600f),
        [4] = (  0f, 300f), [5] = (300f, 300f), [6] = (600f, 300f),
        [7] = (  0f,   0f), [8] = (300f,   0f), [9] = (600f,   0f),
    };

    private readonly string _host;
    private readonly int _port;
    private readonly string _username;
    private readonly string _password;
    private readonly Random _rng = new();

    private TcpClient? _client;
    private NetworkStream? _stream;

    // 송신 시각 큐 (RTT 계산용)
    private readonly ConcurrentQueue<long> _moveSentTimes = new();
    private readonly ConcurrentQueue<long> _chatSentTimes = new();
    private long _ztSentAt;

    public int BotId { get; }
    public int PlayerId { get; private set; }
    public int ZoneId { get; private set; } = 1;
    public float X { get; private set; }
    public float Y { get; private set; }
    public bool Connected { get; private set; }

    // 전송 카운터
    public long MoveRequestsSent { get; private set; }
    public long ZoneTransfersSent { get; private set; }
    public long ZoneTransferResponsesReceived { get; private set; }
    public long ChatRequestsSent { get; private set; }

    // Move RTT
    private long _mvSum, _mvCount, _mvMin = long.MaxValue, _mvMax;
    public long MvAvg => _mvCount > 0 ? _mvSum / _mvCount : 0;
    public long MvMin => _mvCount > 0 ? _mvMin : 0;
    public long MvMax => _mvMax;

    // ZoneTransfer RTT
    private long _ztSum, _ztCount, _ztMin = long.MaxValue, _ztMax;
    public long ZtAvg => _ztCount > 0 ? _ztSum / _ztCount : 0;
    public long ZtMin => _ztCount > 0 ? _ztMin : 0;
    public long ZtMax => _ztMax;

    // Chat RTT
    private long _chatSum, _chatCount, _chatMin = long.MaxValue, _chatMax;
    public long ChatAvg => _chatCount > 0 ? _chatSum / _chatCount : 0;
    public long ChatMin => _chatCount > 0 ? _chatMin : 0;
    public long ChatMax => _chatMax;

    public BotClient(int botId, string host, int port)
    {
        BotId = botId;
        _host = host;
        _port = port;
        _username = $"bot{botId:D4}";
        _password = "botpass123";
    }

    public async Task RunAsync(CancellationToken ct)
    {
        try
        {
            _client = new TcpClient();
            await _client.ConnectAsync(_host, _port, ct);
            _stream = _client.GetStream();
            Connected = true;

            // 로그인 시도 → 실패하면 회원가입 후 재로그인
            if (!await LoginAsync(ct))
            {
                await RegisterAsync(ct);
                await LoginAsync(ct);
            }

            // 랜덤 존으로 먼저 이동 (ZoneTransferResponse 올 때까지 직접 대기)
            int startZone = _rng.Next(1, 10);
            if (startZone != 1)
            {
                SendZoneTransfer(startZone);
                while (true)
                {
                    var (type, body) = await ReadPacketAsync(ct);
                    if (type == 0x0108)
                    {
                        var ztr = ZoneTransferResponse.Parser.ParseFrom(body);
                        if (ztr.Success)
                        {
                            RecordZtRtt();
                            ZoneTransferResponsesReceived++;
                            ZoneId = ztr.ZoneId; X = ztr.SpawnX; Y = ztr.SpawnY;
                        }
                        break;
                    }
                }
            }

            Send(0x0103, Array.Empty<byte>()); // RequestSnapshot
            Send(0x0109, Array.Empty<byte>()); // EnterGame

            var receiveTask = ReceiveLoopAsync(ct);
            await MoveLoopAsync(ct);
            await receiveTask;
        }
        catch (OperationCanceledException) { }
        catch (Exception e) { Console.WriteLine($"[Bot{BotId}] Error: {e.Message}"); }
        finally { _stream?.Close(); _client?.Close(); }
    }

    // ── 로그인 / 회원가입 ─────────────────────────────────────────────────────

    private async Task<bool> LoginAsync(CancellationToken ct)
    {
        Send(0x0001, new LoginRequest { Username = _username, Password = _password }.ToByteArray());
        var (type, body) = await ReadPacketAsync(ct);
        if (type != 0x0002) return false;
        var resp = LoginResponse.Parser.ParseFrom(body);
        if (!resp.Success) return false;
        PlayerId = resp.PlayerId;
        var spawn = SpawnPoints[ZoneId];
        X = spawn.X; Y = spawn.Y;
        return true;
    }

    private async Task RegisterAsync(CancellationToken ct)
    {
        Send(0x0003, new RegisterRequest { Username = _username, Password = _password }.ToByteArray());
        await ReadPacketAsync(ct);
    }

    // ── 이동 루프 ────────────────────────────────────────────────────────────

    private async Task MoveLoopAsync(CancellationToken ct)
    {
        float vx = RandomVelocity();
        float vy = RandomVelocity();
        int tick = 0;
        var moveInterval = TimeSpan.FromMilliseconds(100);

        while (!ct.IsCancellationRequested)
        {
            await Task.Delay(moveInterval, ct);

            X += vx * 0.1f;
            Y += vy * 0.1f;

            if (ZoneBounds.TryGetValue(ZoneId, out var b) && Neighbors.TryGetValue(ZoneId, out var nb))
            {
                int target = -1;
                if      (X > b.X2) target = nb.R;
                else if (X < b.X1) target = nb.L;
                else if (Y > b.Y2) target = nb.U;
                else if (Y < b.Y1) target = nb.D;
                if (target != -1) { SendZoneTransfer(target); continue; }
            }

            if (_rng.NextDouble() < 0.05) vx = RandomVelocity();
            if (_rng.NextDouble() < 0.05) vy = RandomVelocity();

            MoveRequestsSent++;
            _moveSentTimes.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            Send(0x0101, new MoveRequest { X = X, Y = Y, Z = 0f }.ToByteArray());

            ++tick;

            // 5초마다 채팅 전송 (50틱 = 5초)
            if (tick % 50 == 0)
            {
                ChatRequestsSent++;
                _chatSentTimes.Enqueue(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                Send(0x0201, new ChatRequest { Message = $"ping{tick}" }.ToByteArray());
            }

            // 60초마다 Ping 전송 (600틱 = 60초)
            if (tick % 600 == 0)
                Send(0x0011, Array.Empty<byte>()); // Ping
        }
    }

    private void SendZoneTransfer(int targetZoneId)
    {
        ZoneTransfersSent++;
        _ztSentAt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        Send(0x0107, new ZoneTransferRequest { TargetZoneId = targetZoneId }.ToByteArray());
    }

    private void RecordZtRtt()
    {
        if (_ztSentAt == 0) return;
        long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - _ztSentAt;
        _ztSum += rtt; _ztCount++;
        if (rtt < _ztMin) _ztMin = rtt;
        if (rtt > _ztMax) _ztMax = rtt;
    }

    // ── 수신 루프 ────────────────────────────────────────────────────────────

    private async Task ReceiveLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var (type, body) = await ReadPacketAsync(ct);

            switch (type)
            {
                case 0x0102: // MoveResponse — 자신의 응답으로 RTT 측정
                    var mr = MoveResponse.Parser.ParseFrom(body);
                    if (mr.PlayerId == PlayerId && _moveSentTimes.TryDequeue(out long mvSentAt))
                    {
                        long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - mvSentAt;
                        _mvSum += rtt; _mvCount++;
                        if (rtt < _mvMin) _mvMin = rtt;
                        if (rtt > _mvMax) _mvMax = rtt;
                    }
                    break;

                case 0x0108: // ZoneTransferResponse
                    var ztr = ZoneTransferResponse.Parser.ParseFrom(body);
                    if (ztr.Success)
                    {
                        RecordZtRtt();
                        ZoneTransferResponsesReceived++;
                        ZoneId = ztr.ZoneId;
                        X = ztr.SpawnX;
                        Y = ztr.SpawnY;
                        Send(0x0103, Array.Empty<byte>());
                    }
                    break;

                case 0x0202: // ChatBroadcast — 자신의 채팅으로 RTT 측정
                    var cb = ChatBroadcast.Parser.ParseFrom(body);
                    if (cb.PlayerId == PlayerId && _chatSentTimes.TryDequeue(out long chatSentAt))
                    {
                        long rtt = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - chatSentAt;
                        _chatSum += rtt; _chatCount++;
                        if (rtt < _chatMin) _chatMin = rtt;
                        if (rtt > _chatMax) _chatMax = rtt;
                    }
                    break;
            }
        }
    }

    // ── 패킷 I/O ─────────────────────────────────────────────────────────────

    private void Send(ushort type, byte[] body)
    {
        if (_stream == null) return;
        int total = 4 + body.Length;
        var packet = new byte[total];
        packet[0] = (byte)(total & 0xFF);
        packet[1] = (byte)(total >> 8);
        packet[2] = (byte)(type & 0xFF);
        packet[3] = (byte)(type >> 8);
        body.CopyTo(packet, 4);
        lock (_stream) { _stream.Write(packet, 0, total); }
    }

    private async Task<(ushort type, byte[] body)> ReadPacketAsync(CancellationToken ct)
    {
        var header = await ReadExactAsync(4, ct);
        ushort total = (ushort)(header[0] | (header[1] << 8));
        ushort type  = (ushort)(header[2] | (header[3] << 8));
        int bodyLen  = total - 4;
        var body = bodyLen > 0 ? await ReadExactAsync(bodyLen, ct) : Array.Empty<byte>();
        return (type, body);
    }

    private async Task<byte[]> ReadExactAsync(int count, CancellationToken ct)
    {
        var buf = new byte[count];
        int received = 0;
        while (received < count)
        {
            int n = await _stream!.ReadAsync(buf.AsMemory(received, count - received), ct);
            if (n <= 0) throw new Exception("Connection closed");
            received += n;
        }
        return buf;
    }

    private float RandomVelocity() => (float)(_rng.NextDouble() * 40 - 20);
}
