using System;
using System.Collections;
using System.Text;
using GameProto;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.SceneManagement;

public class LoginHandler : MonoBehaviour
{
    public static int PlayerId { get; private set; }
    public static string Token { get; private set; }
    public static string Username { get; private set; }

    [SerializeField] private bool observerMode;

    private LoginUI _loginUI;

    [Serializable] private class HttpLoginRequest  { public string username; public string password; }
    [Serializable] private class HttpLoginResponse { public string token; public int playerId; public string username; }
    [Serializable] private class HttpErrorResponse { public string error; }

    private void Awake()
    {
        _loginUI = GetComponent<LoginUI>();
        GameManager.ObserverMode = observerMode;
    }

    private void Start()
    {
        NetworkManager.Instance.Dispatcher.Register(PacketType.TcpAuthResponse, OnTcpAuthResponse);
    }

    public IEnumerator LoginAsync(string username, string password)
    {
        // 1. HTTP POST /api/auth/login
        var body = JsonUtility.ToJson(new HttpLoginRequest { username = username, password = password });
        using var req = new UnityWebRequest(NetworkManager.Instance.ApiBaseUrl + "/api/auth/login", "POST");
        req.uploadHandler   = new UploadHandlerRaw(Encoding.UTF8.GetBytes(body));
        req.downloadHandler = new DownloadHandlerBuffer();
        req.SetRequestHeader("Content-Type", "application/json");
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            var err = TryParseError(req.downloadHandler.text);
            _loginUI?.ShowError(err ?? req.error);
            yield break;
        }

        var resp = JsonUtility.FromJson<HttpLoginResponse>(req.downloadHandler.text);
        Token    = resp.token;
        PlayerId = resp.playerId;
        Username = observerMode ? "~" + resp.username : resp.username;

        Debug.Log($"[LoginHandler] HTTP login OK. PlayerId={PlayerId}");

        // 2. TCP 연결 후 JWT 전송
        NetworkManager.Instance.Connect();
        yield return new WaitUntil(() => NetworkManager.Instance.IsConnected);

        var jwtBytes = Encoding.UTF8.GetBytes(Token);
        NetworkManager.Instance.Send(PacketType.TcpAuthRequest, jwtBytes);
    }

    private void OnTcpAuthResponse(byte[] body)
    {
        var resp = LoginResponse.Parser.ParseFrom(body);
        if (resp.Success)
        {
            Debug.Log($"[LoginHandler] TCP auth OK. Entering game...");
            SceneManager.LoadScene("TownScene");
        }
        else
        {
            Debug.LogWarning($"[LoginHandler] TCP auth failed: {resp.Token}");
            _loginUI?.ShowError(resp.Token);
            NetworkManager.Instance.Disconnect();
        }
    }

    private static string TryParseError(string json)
    {
        try { return JsonUtility.FromJson<HttpErrorResponse>(json)?.error; }
        catch { return null; }
    }
}
