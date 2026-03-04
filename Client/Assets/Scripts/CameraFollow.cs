using UnityEngine;
using UnityEngine.InputSystem;

public class CameraFollow : MonoBehaviour
{
    [SerializeField] private float smoothSpeed = 10f;
    [SerializeField] private float zoomSpeed = 5f;
    [SerializeField] private float minZoom = 3f;
    [SerializeField] private float maxZoom = 100f;

    private Camera _cam;
    private Transform _target;

    private void Awake()
    {
        _cam = GetComponent<Camera>();
    }

    private void Update()
    {
        var kb = Keyboard.current;

        if (GameManager.ObserverMode)
        {
            // 관찰자 모드: WASD로 카메라 자유 이동
            float h = 0f, v = 0f;
            if (kb != null)
            {
                if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
                if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
                if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;
                if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;
            }
            float panSpeed = _cam.orthographicSize * 2f;
            transform.position += new Vector3(h, v, 0f) * panSpeed * Time.deltaTime;

            // 카메라 위치 기준으로 현재 존 표시
            int zone = GetZoneAt(transform.position.x, transform.position.y);
            if (zone > 0) ZoneUI.Instance?.SetZone(zone);
        }
        else
        {
            if (_target == null)
            {
                var lp = FindFirstObjectByType<LocalPlayer>();
                if (lp != null) _target = lp.transform;
                return;
            }

            var dest = new Vector3(_target.position.x, _target.position.y, transform.position.z);
            transform.position = Vector3.Lerp(transform.position, dest, smoothSpeed * Time.deltaTime);
        }

        // 스크롤 줌인/줌아웃
        var mouse = Mouse.current;
        if (mouse != null)
        {
            float scroll = mouse.scroll.ReadValue().y;
            if (scroll != 0f)
                _cam.orthographicSize = Mathf.Clamp(_cam.orthographicSize - scroll * zoomSpeed * 0.01f, minZoom, maxZoom);
        }
    }

    // 3x3 그리드 존 감지
    private static int GetZoneAt(float x, float y)
    {
        if (x >= -140 && x <= 140 && y >= 460 && y <= 740) return 1;
        if (x >=  160 && x <= 440 && y >= 460 && y <= 740) return 2;
        if (x >=  460 && x <= 740 && y >= 460 && y <= 740) return 3;
        if (x >= -140 && x <= 140 && y >= 160 && y <= 440) return 4;
        if (x >=  160 && x <= 440 && y >= 160 && y <= 440) return 5;
        if (x >=  460 && x <= 740 && y >= 160 && y <= 440) return 6;
        if (x >= -140 && x <= 140 && y >= -140 && y <= 140) return 7;
        if (x >=  160 && x <= 440 && y >= -140 && y <= 140) return 8;
        if (x >=  460 && x <= 740 && y >= -140 && y <= 140) return 9;
        return -1; // 존 경계 사이 갭
    }
}
