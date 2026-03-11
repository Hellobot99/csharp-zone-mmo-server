using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

public class RegisterHandler : MonoBehaviour
{
    private LoginUI _loginUI;

    [Serializable] private class HttpRegisterRequest  { public string username; public string password; }
    [Serializable] private class HttpRegisterResponse { public int playerId; public string username; }
    [Serializable] private class HttpErrorResponse    { public string error; }

    private void Awake()
    {
        _loginUI = GetComponent<LoginUI>();
    }

    public IEnumerator RegisterAsync(string username, string password)
    {
        var body = JsonUtility.ToJson(new HttpRegisterRequest { username = username, password = password });
        using var req = new UnityWebRequest(NetworkManager.Instance.ApiBaseUrl + "/api/auth/register", "POST");
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

        Debug.Log("[RegisterHandler] Registration successful.");
        _loginUI?.ShowMessage("Registration successful! Please log in.");
    }

    private static string TryParseError(string json)
    {
        try { return JsonUtility.FromJson<HttpErrorResponse>(json)?.error; }
        catch { return null; }
    }
}
