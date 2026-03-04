using GameProto;
using Google.Protobuf;
using UnityEngine;

public class RegisterHandler : MonoBehaviour
{
    private LoginUI _loginUI;

    private void Awake()
    {
        _loginUI = GetComponent<LoginUI>();
    }

    private void Start()
    {
        NetworkManager.Instance.Dispatcher.Register(PacketType.RegisterResponse, OnRegisterResponse);
    }

    public void SendRegisterRequest(string username, string password)
    {
        var req = new RegisterRequest { Username = username, Password = password };
        NetworkManager.Instance.Send(PacketType.RegisterRequest, req.ToByteArray());
    }

    private void OnRegisterResponse(byte[] body)
    {
        var resp = RegisterResponse.Parser.ParseFrom(body);
        if (resp.Success)
        {
            Debug.Log($"[RegisterHandler] Register success. PlayerId={resp.PlayerId}");
            _loginUI?.ShowMessage("Registration successful! Please log in.");
        }
        else
        {
            Debug.LogWarning($"[RegisterHandler] Register failed: {resp.Token}");
            _loginUI?.ShowError(resp.Token);
        }
    }
}
