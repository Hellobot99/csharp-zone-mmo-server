using GameProto;
using Google.Protobuf;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoginHandler : MonoBehaviour
{
    public static int PlayerId { get; private set; }
    public static string Token { get; private set; }
    public static string Username { get; private set; }

    [SerializeField] private bool observerMode;

    private LoginUI _loginUI;

    private void Awake()
    {
        _loginUI = GetComponent<LoginUI>();
        GameManager.ObserverMode = observerMode;
    }

    private void Start()
    {
        NetworkManager.Instance.Dispatcher.Register(PacketType.LoginResponse, OnLoginResponse);
    }

    public void SendLoginRequest(string username, string password)
    {
        Username = username;
        var sendUsername = observerMode ? "~" + username : username;
        var req = new LoginRequest { Username = sendUsername, Password = password };
        NetworkManager.Instance.Send(PacketType.LoginRequest, req.ToByteArray());
    }

    private void OnLoginResponse(byte[] body)
    {
        var resp = LoginResponse.Parser.ParseFrom(body);
        if (resp.Success)
        {
            PlayerId = resp.PlayerId;
            Token = resp.Token;
            Debug.Log($"[LoginHandler] Login success. PlayerId={PlayerId}");
            SceneManager.LoadScene("TownScene");
        }
        else
        {
            Debug.LogWarning($"[LoginHandler] Login failed: {resp.Token}");
            _loginUI?.ShowError(resp.Token);
        }
    }
}
