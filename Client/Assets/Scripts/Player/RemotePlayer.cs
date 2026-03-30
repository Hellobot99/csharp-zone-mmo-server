using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 10f;

    private static readonly Color[] PlayerColors = new[]
    {
        Color.white, Color.red, Color.blue, Color.green,
        Color.yellow, Color.magenta, Color.cyan
    };

    private Vector3 _targetPosition;
    private bool _hasTarget;
    private SpriteRenderer _sr;

    public string Username { get; private set; }

    private void Awake()
    {
        _targetPosition = transform.position;
        _sr = GetComponent<SpriteRenderer>();
    }

    private void Update()
    {
        if (_hasTarget)
            transform.position = Vector3.Lerp(transform.position, _targetPosition, lerpSpeed * Time.deltaTime);
    }

    public void Setup(string username)
    {
        Username = username;
    }

    public void SetTargetPosition(float x, float y, float z)
    {
        _targetPosition = new Vector3(x, y, 0f);
        _hasTarget = true;
    }

    public void SetColor(int colorIndex)
    {
        if (_sr == null) return;
        _sr.color = PlayerColors[colorIndex % PlayerColors.Length];
    }

    public void OnDeath()
    {
        if (_sr != null) _sr.color = new Color(0.3f, 0.3f, 0.3f);
    }

    public void OnRespawn(float x, float y)
    {
        _targetPosition = new Vector3(x, y, 0f);
        transform.position = _targetPosition;
        if (_sr != null) _sr.color = Color.white;
    }
}
