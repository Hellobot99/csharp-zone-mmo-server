using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static bool ObserverMode { get; internal set; }

    [SerializeField] private GameObject localPlayerPrefab;
    [SerializeField] private Vector3 spawnPosition = new Vector3(0, 600, 0);

    private void Start()
    {
        if (!ObserverMode)
            SpawnLocalPlayer();
    }

    private void SpawnLocalPlayer()
    {
        if (localPlayerPrefab == null)
        {
            Debug.LogError("[GameManager] localPlayerPrefab is not assigned!");
            return;
        }

        if (FindFirstObjectByType<LocalPlayer>() != null) return;

        var go = Instantiate(localPlayerPrefab, spawnPosition, Quaternion.identity);
        go.name = "LocalPlayer";
    }
}
