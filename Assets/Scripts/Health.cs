using System.Collections;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(Collider2D))]
public class Health : MonoBehaviour, IDamageable
{
    [Header("Health")]
    [SerializeField] private int maxHealth = 100;
    [SerializeField] private bool destroyOnDeath = false;

    [Header("Knockback")]
    [SerializeField] private float knockbackForce = 6f;         // impulse
    [SerializeField] private float knockbackUpBias = 0f;        // small Y bias if you like

    [Header("Invulnerability Frames")]
    [SerializeField] private float invulnDuration = 0.3f;
    [SerializeField] private bool flashDuringIFrames = true;
    [SerializeField] private float flashInterval = 0.06f;

    [Header("Hit Effect")]
    [SerializeField] private GameObject hitEffectPrefab;         // optional VFX prefab
    [SerializeField] private float hitEffectLifetime = 0.6f;


    public UnityEvent onDamaged;
    public UnityEvent onDeath;

    public int CurrentHealth { get; private set; }
    public bool IsAlive => CurrentHealth > 0;

    // cache
    private Rigidbody2D rb;
    private Collider2D col;
    private SpriteRenderer sr;   // for flash
    private bool invulnerable;

    private Coroutine _ifrCo;

    public bool IsInvulnerable => invulnerable;

    private void Awake()
    {
        CurrentHealth = Mathf.Max(1, maxHealth);
        rb = GetComponent<Rigidbody2D>();          // may be null on static props, that’s fine
        col = GetComponent<Collider2D>();
        sr = GetComponentInChildren<SpriteRenderer>();
    }

    public void TakeDamage(int amount, Vector2 hitFrom)
    {
        if (invulnerable || !IsAlive) return;

        // apply damage
        CurrentHealth = Mathf.Max(0, CurrentHealth - Mathf.Abs(amount));
        onDamaged?.Invoke();

        // spawn hit effect at closest collider point to attacker
        if (hitEffectPrefab && col)
        {
            Vector2 contact = col.ClosestPoint(hitFrom);
            var vfx = Instantiate(hitEffectPrefab, contact, Quaternion.identity);
            if (hitEffectLifetime > 0f) Destroy(vfx, hitEffectLifetime);
        }

        // knockback (if we have a rigidbody)
        if (rb)
        {
            Vector2 dir = ((Vector2)transform.position - hitFrom);
            if (dir.sqrMagnitude < 1e-6f) dir = Vector2.right;
            dir.Normalize();
            dir.y += knockbackUpBias; // optional tilt
            rb.AddForce(dir.normalized * knockbackForce, ForceMode2D.Impulse);
        }

        // iFrames
        if (invulnDuration > 0f) StartCoroutine(IFrames(0.5f));

        // death
        if (CurrentHealth == 0)
        {
            onDeath?.Invoke();
            if (destroyOnDeath) Destroy(gameObject);
            // else you can disable AI/movement here as needed
        }
    }

    public void Heal(int amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Min(maxHealth, CurrentHealth + Mathf.Abs(amount));
    }

    private IEnumerator IFrames(float duration)
    {
        invulnerable = true;

        if (flashDuringIFrames && sr)
        {
            float t = 0f;
            bool on = true;
            Color baseCol = sr.color;
            while (t < duration)
            {
                sr.color = on ? new Color(baseCol.r, baseCol.g, baseCol.b, 0.35f) : baseCol;
                on = !on;
                yield return new WaitForSeconds(flashInterval);
                t += flashInterval;
            }
            sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
        }
        else
        {
            yield return new WaitForSeconds(duration);
        }

        invulnerable = false;
        _ifrCo = null;
    }

    private void EndIFramesVisual()
    {
        if (sr) sr.color = new Color(sr.color.r, sr.color.g, sr.color.b, 1f);
    }

    private void StopCoroutineSafe(Coroutine c)
    {
        if (c != null) StopCoroutine(c);
    }

    public void TriggerIFrames(float duration)
    {
        if (duration <= 0f) return;
        // If already invulnerable, restart with the new duration
        StopCoroutineSafe(_ifrCo);
        _ifrCo = StartCoroutine(IFrames(duration));
    }

    public void CancelIFrames()
    {
        if (!invulnerable) return;
        StopCoroutineSafe(_ifrCo);
        EndIFramesVisual();
        invulnerable = false;
    }
}
