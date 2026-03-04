using TMPro;
using UnityEngine;

public class ZoneUI : MonoBehaviour
{
    public static ZoneUI Instance { get; private set; }

    [SerializeField] private TMP_Text zoneText;

    private void Awake()
    {
        Instance = this;
    }

    public void SetZone(int zoneId)
    {
        zoneText.text = $"Zone {zoneId}";
    }
}
