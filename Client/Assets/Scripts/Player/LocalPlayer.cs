using GameProto;
using Google.Protobuf;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

public class LocalPlayer : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 5f;
    [SerializeField] private float sendInterval = 0.1f;

    private Rigidbody2D _rb;
    private float _sendTimer;
    private Vector3 _lastSentPosition;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        if (!Application.isFocused) return;
        if (IsChatFocused()) return;

        HandleMovement();
        TrySendPosition();
    }

    private bool IsChatFocused()
    {
        var selected = UnityEngine.EventSystems.EventSystem.current?.currentSelectedGameObject;
        if (selected == null) return false;
        var inputField = selected.GetComponent<TMP_InputField>();
        return inputField != null && inputField.isFocused;
    }

    public bool IsDead { get; private set; }

    public void FlashDamage()
    {
        if (!IsDead) StartCoroutine(DamageFlash());
    }

    private System.Collections.IEnumerator DamageFlash()
    {
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        if (!IsDead && sr != null) sr.color = Color.white;
    }

    public void OnDeath()
    {
        IsDead = true;
        StopAllCoroutines();
        _rb.linearVelocity = Vector2.zero;
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = new Color(0.3f, 0.3f, 0.3f);
    }

    public void OnRespawn(float x, float y)
    {
        IsDead = false;
        _rb.position = new Vector2(x, y);
        var sr = GetComponent<SpriteRenderer>();
        if (sr != null) sr.color = Color.white;
    }

    private void HandleMovement()
    {
        if (IsDead) return;

        var kb = Keyboard.current;
        if (kb == null) return;

        float h = 0f, v = 0f;

        if (kb.aKey.isPressed || kb.leftArrowKey.isPressed)  h = -1f;
        if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) h =  1f;
        if (kb.sKey.isPressed || kb.downArrowKey.isPressed)  v = -1f;
        if (kb.wKey.isPressed || kb.upArrowKey.isPressed)    v =  1f;

        _rb.linearVelocity = new Vector2(h, v) * moveSpeed;
    }

    private void TrySendPosition()
    {
        if (NetworkManager.Instance == null) return;

        _sendTimer += Time.deltaTime;
        if (_sendTimer < sendInterval) return;
        _sendTimer = 0f;

        if (transform.position == _lastSentPosition) return;
        _lastSentPosition = transform.position;

        var req = new MoveRequest
        {
            X = transform.position.x,
            Y = transform.position.y,
            Z = 0f
        };
        NetworkManager.Instance.Send(PacketType.MoveRequest, req.ToByteArray());
    }
}
