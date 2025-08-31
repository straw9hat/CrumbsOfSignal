using UnityEngine;

public class GameOverWatcher : MonoBehaviour
{
    [SerializeField] private Health playerHealth;
    [SerializeField] private Health spatulaHealth;

    void Awake()
    {
        // Best-effort auto-find if not wired
        if (!playerHealth)
        {
            var p = GameObject.FindGameObjectWithTag("Player");
            if (p) playerHealth = p.GetComponentInChildren<Health>();
        }
        if (!spatulaHealth)
        {
            var s = GameObject.FindGameObjectWithTag("Spatula");
            if (s) spatulaHealth = s.GetComponentInChildren<Health>();
        }
    }

    void OnEnable()
    {
        if (playerHealth) playerHealth.onDeath.AddListener(OnPlayerDead);
        if (spatulaHealth) spatulaHealth.onDeath.AddListener(OnSpatulaDead);
    }
    void OnDisable()
    {
        if (playerHealth) playerHealth.onDeath.RemoveListener(OnPlayerDead);
        if (spatulaHealth) spatulaHealth.onDeath.RemoveListener(OnSpatulaDead);
    }

    void OnPlayerDead() => GameOverManager.Instance?.TriggerGameOver("Player defeated");
    void OnSpatulaDead() => GameOverManager.Instance?.TriggerGameOver("Spatula destroyed");
}
