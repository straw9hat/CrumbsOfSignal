using UnityEngine;

[RequireComponent(typeof(Health))]
public class KillRewards : MonoBehaviour
{
    [Header("Reward")]
    [SerializeField] private bool isBoss = false;
    [SerializeField] private int normalKillPoints = 2;
    [SerializeField] private int bossKillPoints = 10;

    [Header("Drop")]
    [SerializeField] private GameObject cookiePickupPrefab; // has CookiePickup.cs

    private Health health;

    void Awake()
    {
        health = GetComponent<Health>();
        // Wire to Health events
        health.onDeath.AddListener(OnKilled);
    }

    private void OnKilled()
    {
        // 1) award score
        ScoreManager.Instance?.AddPoints(isBoss ? bossKillPoints : normalKillPoints);

        // 2) drop cookie
        if (cookiePickupPrefab)
            Instantiate(cookiePickupPrefab, transform.position, Quaternion.identity);
    }
}
