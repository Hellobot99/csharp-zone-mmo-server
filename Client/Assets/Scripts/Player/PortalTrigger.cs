using GameProto;
using Google.Protobuf;
using UnityEngine;

public class PortalTrigger : MonoBehaviour
{
    [SerializeField] private int targetZoneId;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!other.CompareTag("LocalPlayer")) return;

        var req = new ZoneTransferRequest { TargetZoneId = targetZoneId };
        NetworkManager.Instance.Send(PacketType.ZoneTransferRequest, req.ToByteArray());
    }
}
