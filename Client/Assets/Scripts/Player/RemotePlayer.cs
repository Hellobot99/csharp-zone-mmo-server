using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 10f;

    private Vector3 _targetPosition;
    private bool _hasTarget;

    public string Username { get; private set; }

    private void Awake()
    {
        _targetPosition = transform.position;
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
}
