using System;
using System.Collections.Generic;
using GameProto;
using UnityEngine;

public class PlayerManager : MonoBehaviour
{
    [SerializeField] private GameObject remotePlayerPrefab;

    private readonly Dictionary<int, RemotePlayer> _remotePlayers = new();

    private void Start()
    {
        NetworkManager.Instance.Dispatcher.Register(PacketType.ZoneSnapshot,         OnZoneSnapshot);
        NetworkManager.Instance.Dispatcher.Register(PacketType.PlayerEnter,          OnPlayerEnter);
        NetworkManager.Instance.Dispatcher.Register(PacketType.PlayerLeave,          OnPlayerLeave);
        NetworkManager.Instance.Dispatcher.Register(PacketType.MoveResponse,         OnMoveResponse);
        NetworkManager.Instance.Dispatcher.Register(PacketType.ZoneTransferResponse, OnZoneTransfer);
        NetworkManager.Instance.Dispatcher.Register(PacketType.SkillResponse,        OnSkillResponse);
        NetworkManager.Instance.Dispatcher.Register(PacketType.DeathResponse,        OnDeathResponse);
        NetworkManager.Instance.Dispatcher.Register(PacketType.RespawnResponse,      OnRespawnResponse);

        // GameScene 로드 후 핸들러가 준비된 시점에 스냅샷 요청 + 게임 입장 알림
        NetworkManager.Instance.Send(PacketType.RequestSnapshot, Array.Empty<byte>());
        if (!GameManager.ObserverMode)
            NetworkManager.Instance.Send(PacketType.EnterGame, Array.Empty<byte>());
        ZoneUI.Instance?.SetZone(1);
    }

    // ── 핸들러 ──────────────────────────────────────────────────────────────────

    private void OnZoneSnapshot(byte[] body)
    {
        var snapshot = ZoneSnapshot.Parser.ParseFrom(body);
        foreach (var p in snapshot.Players)
        {
            if (!GameManager.ObserverMode && p.PlayerId == LoginHandler.PlayerId) continue;
            SpawnRemotePlayer(p.PlayerId, p.Username, p.X, p.Y);
        }
    }

    private void OnPlayerEnter(byte[] body)
    {
        var p = GameProto.PlayerEnter.Parser.ParseFrom(body);
        if (!GameManager.ObserverMode && p.PlayerId == LoginHandler.PlayerId) return;
        SpawnRemotePlayer(p.PlayerId, p.Username, p.X, p.Y);
    }

    private void OnPlayerLeave(byte[] body)
    {
        var p = GameProto.PlayerLeave.Parser.ParseFrom(body);
        RemoveRemotePlayer(p.PlayerId);
    }

    private void OnMoveResponse(byte[] body)
    {
        var resp = MoveResponse.Parser.ParseFrom(body);
        if (!GameManager.ObserverMode && resp.PlayerId == LoginHandler.PlayerId) return;

        if (_remotePlayers.TryGetValue(resp.PlayerId, out var player))
            player.SetTargetPosition(resp.X, resp.Y, resp.Z);
    }

    private void OnZoneTransfer(byte[] body)
    {
        var resp = ZoneTransferResponse.Parser.ParseFrom(body);
        if (!resp.Success) return;

        foreach (var p in _remotePlayers.Values)
            Destroy(p.gameObject);
        _remotePlayers.Clear();

        if (!GameManager.ObserverMode)
        {
            var localPlayer = FindFirstObjectByType<LocalPlayer>();
            if (localPlayer != null)
            {
                var rb = localPlayer.GetComponent<Rigidbody2D>();
                if (rb != null) rb.position = new Vector2(resp.SpawnX, resp.SpawnY);
                else localPlayer.transform.position = new Vector3(resp.SpawnX, resp.SpawnY, 0f);
            }
        }

        ZoneUI.Instance?.SetZone(resp.ZoneId);
        NetworkManager.Instance.Send(PacketType.RequestSnapshot, Array.Empty<byte>());
    }

    private void OnSkillResponse(byte[] body)
    {
        var resp = GameProto.SkillResponse.Parser.ParseFrom(body);
        if (!GameManager.ObserverMode && resp.PlayerId == LoginHandler.PlayerId)
        {
            var local = FindFirstObjectByType<LocalPlayer>();
            if (local != null)
                local.SetColor(resp.ColorIndex);
            return;
        }
        if (_remotePlayers.TryGetValue(resp.PlayerId, out var player))
            player.SetColor(resp.ColorIndex);
    }

    private void OnDeathResponse(byte[] body)
    {
        var resp = GameProto.DeathResponse.Parser.ParseFrom(body);
        if (!GameManager.ObserverMode && resp.DeadPlayerId == LoginHandler.PlayerId)
        {
            FindFirstObjectByType<LocalPlayer>()?.OnDeath();
            return;
        }
        if (_remotePlayers.TryGetValue(resp.DeadPlayerId, out var player))
            player.OnDeath();
    }

    private void OnRespawnResponse(byte[] body)
    {
        var resp = GameProto.RespawnResponse.Parser.ParseFrom(body);
        if (!GameManager.ObserverMode && resp.PlayerId == LoginHandler.PlayerId)
        {
            FindFirstObjectByType<LocalPlayer>()?.OnRespawn(resp.X, resp.Y);
            return;
        }
        if (_remotePlayers.TryGetValue(resp.PlayerId, out var player))
            player.OnRespawn(resp.X, resp.Y);
    }

    // ── 헬퍼 ────────────────────────────────────────────────────────────────────

    private void SpawnRemotePlayer(int playerId, string username, float x, float y)
    {
        if (_remotePlayers.ContainsKey(playerId)) return;

        var go = Instantiate(remotePlayerPrefab, new Vector3(x, y, 0f), Quaternion.identity);
        go.name = $"RemotePlayer_{username}";

        // LocalPlayer 컴포넌트가 붙어있으면 제거 (잘못된 prefab 할당 방어)
        var localPlayer = go.GetComponent<LocalPlayer>();
        if (localPlayer != null) Destroy(localPlayer);

        var player = go.GetComponent<RemotePlayer>() ?? go.AddComponent<RemotePlayer>();
        player.Setup(username);
        _remotePlayers[playerId] = player;
    }

    private void RemoveRemotePlayer(int playerId)
    {
        if (_remotePlayers.TryGetValue(playerId, out var player))
        {
            Destroy(player.gameObject);
            _remotePlayers.Remove(playerId);
        }
    }
}
