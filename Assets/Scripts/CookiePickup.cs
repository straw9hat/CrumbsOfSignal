using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class CookiePickup : MonoBehaviour
{
    [SerializeField] private int bonusPoints = 5;
    [SerializeField] private int healAmount = 20;   // NEW: heal player
    [SerializeField] private float lifetime = 2f;
    [SerializeField] private AnimationCurve fade = AnimationCurve.Linear(0, 1, 1, 0);

    private float t;
    private SpriteRenderer sr;
    private bool collected;

    void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
        gameObject.layer = LayerMask.NameToLayer("Pickup"); // optional
    }

    void Awake() => sr = GetComponentInChildren<SpriteRenderer>();

    void Update()
    {
        t += Time.deltaTime;
        if (sr)
        {
            float a = fade.Evaluate(Mathf.Clamp01(t / lifetime));
            var c = sr.color; c.a = a; sr.color = c;
        }
        if (t >= lifetime && !collected) Destroy(gameObject);
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (collected) return;
        var player = other.GetComponentInParent<PlayerController>();
        if (!player) return;

        collected = true;

        // 1) give score
        ScoreManager.Instance?.AddPoints(bonusPoints);

        // 2) heal player
        var health = player.GetComponent<Health>();
        if (health) health.Heal(healAmount);   // assumes Health has Heal(int)

        Destroy(gameObject);
    }
}
