using System;
using System.Collections.Concurrent;
using System.Net.Sockets;
using System.Threading;
using UnityEngine;

public class NetworkManager : MonoBehaviour
{
    public static NetworkManager Instance { get; private set; }

    [SerializeField] private string host = "127.0.0.1";
    [SerializeField] private int port = 7000;

    private TcpClient _client;
    private NetworkStream _stream;
    private Thread _receiveThread;
    private volatile bool _isRunning;

    private readonly ConcurrentQueue<(ushort packetType, byte[] body)> _receiveQueue = new();

    public PacketDispatcher Dispatcher { get; private set; }

    private void Awake()
    {
        if (Instance != null)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        Dispatcher = new PacketDispatcher();
        Application.runInBackground = true; // 포커스 없을 때도 Update() 실행
    }

    public void Connect()
    {
        try
        {
            _client = new TcpClient();
            _client.Connect(host, port);
            _stream = _client.GetStream();
            _isRunning = true;

            _receiveThread = new Thread(ReceiveLoop) { IsBackground = true };
            _receiveThread.Start();

            Debug.Log($"[NetworkManager] Connected to {host}:{port}");
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManager] Connect failed: {e.Message}");
        }
    }

    public void Disconnect()
    {
        _isRunning = false;
        _stream?.Close();
        _client?.Close();
    }

    /// <summary>
    /// Build and send a framed packet: [TotalSize:2 LE][PacketType:2 LE][Body:n]
    /// TotalSize includes the 4-byte header.
    /// </summary>
    public void Send(ushort packetType, byte[] body)
    {
        if (_stream == null || !_client.Connected)
        {
            Debug.LogError($"[NetworkManager] Send 0x{packetType:X4} failed: not connected");
            return;
        }

        int totalSize = 4 + (body?.Length ?? 0);
        byte[] packet = new byte[totalSize];

        packet[0] = (byte)(totalSize & 0xFF);
        packet[1] = (byte)((totalSize >> 8) & 0xFF);
        packet[2] = (byte)(packetType & 0xFF);
        packet[3] = (byte)((packetType >> 8) & 0xFF);

        if (body != null && body.Length > 0)
            Buffer.BlockCopy(body, 0, packet, 4, body.Length);

        try
        {
            lock (_stream)
            {
                _stream.Write(packet, 0, packet.Length);
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"[NetworkManager] Send error: {e.Message}");
        }
    }

    // Runs on background thread — no Unity API calls
    private void ReceiveLoop()
    {
        byte[] headerBuf = new byte[4];

        while (_isRunning)
        {
            try
            {
                if (!ReadExact(headerBuf, 0, 4)) break;

                ushort totalSize  = (ushort)(headerBuf[0] | (headerBuf[1] << 8));
                ushort packetType = (ushort)(headerBuf[2] | (headerBuf[3] << 8));
                int bodyLen = totalSize - 4;

                byte[] body = bodyLen > 0 ? new byte[bodyLen] : Array.Empty<byte>();
                if (bodyLen > 0 && !ReadExact(body, 0, bodyLen)) break;

                _receiveQueue.Enqueue((packetType, body));
            }
            catch (Exception e)
            {
                if (_isRunning)
                    UnityEngine.Debug.LogError($"[NetworkManager] Receive error: {e.Message}");
                break;
            }
        }
    }

    private bool ReadExact(byte[] buf, int offset, int count)
    {
        int received = 0;
        while (received < count)
        {
            int n = _stream.Read(buf, offset + received, count - received);
            if (n <= 0) return false;
            received += n;
        }
        return true;
    }

    // Main thread: dequeue and dispatch received packets
    private void Update()
    {
        while (_receiveQueue.TryDequeue(out var packet))
            Dispatcher.Dispatch(packet.packetType, packet.body);
    }

    private void OnDestroy()
    {
        Disconnect();
    }
}
