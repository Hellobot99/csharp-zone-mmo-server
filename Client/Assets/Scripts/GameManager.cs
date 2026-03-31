using GameProto;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static bool ObserverMode { get; internal set; }

    [SerializeField] private GameObject localPlayerPrefab;

    private void Awake()
    {
        NetworkManager.Instance.Dispatcher.Register(PacketType.SpawnResponse, OnSpawnResponse);
    }

    private void OnSpawnResponse(byte[] body)
    {
        var resp = SpawnResponse.Parser.ParseFrom(body);
        if (!ObserverMode)
            SpawnLocalPlayer(resp.X, resp.Y);
    }

    private void SpawnLocalPlayer(float x, float y)
    {
        if (localPlayerPrefab == null)
        {
            Debug.LogError("[GameManager] localPlayerPrefab is not assigned!");
            return;
        }

        if (FindFirstObjectByType<LocalPlayer>() != null) return;

        var go = Instantiate(localPlayerPrefab, new Vector3(x, y, 0f), Quaternion.identity);
        go.name = "LocalPlayer";
    }
}
