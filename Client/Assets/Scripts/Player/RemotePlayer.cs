using System.Collections;
using UnityEngine;

public class RemotePlayer : MonoBehaviour
{
    [SerializeField] private float lerpSpeed = 10f;

    private Vector3 _targetPosition;
    private bool _hasTarget;
    private SpriteRenderer _sr;
    private bool _isDead;

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

    public void FlashDamage()
    {
        if (!_isDead) StartCoroutine(DamageFlash());
    }

    private IEnumerator DamageFlash()
    {
        if (_sr != null) _sr.color = Color.red;
        yield return new WaitForSeconds(0.2f);
        if (!_isDead && _sr != null) _sr.color = Color.white;
    }

    public void OnDeath()
    {
        _isDead = true;
        StopAllCoroutines();
        if (_sr != null) _sr.color = new Color(0.3f, 0.3f, 0.3f);
    }

    public void OnRespawn(float x, float y)
    {
        _isDead = false;
        _targetPosition = new Vector3(x, y, 0f);
        transform.position = _targetPosition;
        if (_sr != null) _sr.color = Color.white;
    }
}
