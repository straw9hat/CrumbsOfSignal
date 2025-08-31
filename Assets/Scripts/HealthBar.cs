using UnityEngine;
using UnityEngine.UI;

public class HealthBar : MonoBehaviour
{
    [Header("Wiring")]
    [SerializeField] private Health targetHealth;     // auto-found if null
    [SerializeField] private Transform target;        // whose head to follow (auto if null)
    [SerializeField] private Image fill;              // the filled image
    [SerializeField] private CanvasGroup canvasGroup; // optional

    [Header("Behavior")]
    [SerializeField] private Vector2 worldOffset = new Vector2(0f, 1.0f);
    [SerializeField] private bool billboardToCamera = true;
    [SerializeField] private bool hideWhenFull = true;
    [SerializeField] private float lerpSpeed = 12f;
    [SerializeField] private Gradient colorByHealth;  // green?yellow?red; can be null

    Camera cam;
    int lastCur = -1, lastMax = -1;
    float displayed = 1f;

    void Awake()
    {
        cam = Camera.main;
        if (!targetHealth) targetHealth = GetComponentInParent<Health>();
        if (!target) target = targetHealth ? targetHealth.transform : transform.parent;
        if (!canvasGroup) canvasGroup = GetComponent<CanvasGroup>();
        if (!fill) fill = GetComponentInChildren<Image>();
    }

    void OnEnable()
    {
        // subscribe if dev added the event
        if (targetHealth && targetHealth.onHealthChanged != null)
            targetHealth.onHealthChanged.AddListener(OnHealthChanged);

        // also listen for death to hide instantly
        if (targetHealth) targetHealth.onDeath.AddListener(HideInstant);
        ForceRefresh();
    }

    void OnDisable()
    {
        if (targetHealth && targetHealth.onHealthChanged != null)
            targetHealth.onHealthChanged.RemoveListener(OnHealthChanged);
        if (targetHealth) targetHealth.onDeath.RemoveListener(HideInstant);
    }

    void LateUpdate()
    {
        // Follow target head
        if (target)
        {
            Vector3 pos = target.position + (Vector3)worldOffset;
            // try to place above sprite bounds if available
            var sr = target.GetComponentInChildren<SpriteRenderer>();
            if (sr) pos = sr.bounds.max + new Vector3(0f, 0.15f, 0f);

            transform.position = pos;
        }

        // Billboard
        if (billboardToCamera && cam)
        {
            // Copy camera rotation so the bar faces screen
            transform.rotation = cam.transform.rotation;
        }

        // Poll if no onHealthChanged wired
        if (targetHealth && (targetHealth.CurrentHealth != lastCur || targetHealth.MaxHealth != lastMax))
        {
            Apply(targetHealth.CurrentHealth, targetHealth.MaxHealth, immediate: false);
        }

        // Smooth fill
        if (fill)
        {
            fill.fillAmount = Mathf.MoveTowards(fill.fillAmount, displayed, lerpSpeed * Time.deltaTime);
        }
    }

    void OnHealthChanged(int cur, int max) => Apply(cur, max, immediate: false);

    void Apply(int cur, int max, bool immediate)
    {
        lastCur = cur; lastMax = Mathf.Max(1, max);
        float t = Mathf.Clamp01((float)cur / lastMax);
        displayed = t;

        if (immediate && fill) fill.fillAmount = t;

        if (colorByHealth != null && fill)
            fill.color = colorByHealth.Evaluate(t);
        else if (fill)
            fill.color = Color.Lerp(Color.red, Color.green, t);

        bool visible = !(hideWhenFull && t >= 0.999f) && cur > 0;
        if (canvasGroup) canvasGroup.alpha = visible ? 1f : 0f;
        else gameObject.SetActive(visible);
    }

    public void ForceRefresh()
    {
        if (!targetHealth) return;
        Apply(targetHealth.CurrentHealth, targetHealth.MaxHealth, immediate: true);
    }

    void HideInstant()
    {
        if (canvasGroup) canvasGroup.alpha = 0f;
        else gameObject.SetActive(false);
    }
}
