using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class EnemyController : MonoBehaviour
{
    [Header("Targets")]
    [SerializeField] private Transform player;        // assign in Inspector (or auto-find by tag "Player")
    [SerializeField] private Transform spatula;       // assign in Inspector (or auto-find by tag "Spatula")
    [SerializeField] private float retargetInterval = 0.2f;
    [SerializeField] private float switchHysteresis = 0.5f; // only switch if the other target is at least this much closer

    private Transform currentTarget;
    private float nextRetargetTime;

    [Header("Movement")]
    [SerializeField] private float moveSpeed = 2.8f;
    [SerializeField] private float acceleration = 30f;
    [SerializeField] private float stopDistance = 1.0f;

    [Header("Melee")]
    [SerializeField] private int meleeDamage = 10;
    [SerializeField] private float meleeCooldown = 0.6f;
    [SerializeField] private float meleeReach = 1.0f;
    [SerializeField] private float meleeRadius = 0.9f;
    [SerializeField] private float meleeArcDeg = 160f;
    [SerializeField] private LayerMask meleeTargets; // include Player + Spatula layers

    [Header("FX/Anim (optional)")]
    //[SerializeField] private Animator animator;

    private Rigidbody2D rb;
    private Health health;
    private float lastAttackTime = -999f;
    private Vector2 currentVelocity;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        rb.constraints |= RigidbodyConstraints2D.FreezeRotation;

        health = GetComponent<Health>();

        // Best-effort auto-find if not wired in the Inspector
        if (!player)
        {
            var go = GameObject.FindGameObjectWithTag("Player");
            if (go) player = go.transform;
        }
        if (!spatula)
        {
            var go = GameObject.FindGameObjectWithTag("Spatula");
            if (go) spatula = go.transform;
        }
    }

    private void FixedUpdate()
    {
        if (!health.IsAlive)
        {
            rb.linearVelocity = Vector2.zero;
            return;
        }

        // Retarget periodically
        if (Time.time >= nextRetargetTime)
        {
            ChooseTarget();
            nextRetargetTime = Time.time + retargetInterval;
        }

        // If nothing to chase, idle
        if (!currentTarget)
        {
            rb.linearVelocity = Vector2.zero;
            //if (animator) animator.SetFloat("Speed", 0f);
            return;
        }

        // --- Movement toward target ---
        Vector2 toTarget = (Vector2)currentTarget.position - (Vector2)transform.position;
        float dist = toTarget.magnitude;
        Vector2 desiredVel = dist > stopDistance ? toTarget.normalized * moveSpeed : Vector2.zero;

        Vector2 diff = desiredVel - rb.linearVelocity;
        Vector2 change = Vector2.ClampMagnitude(diff, acceleration * Time.fixedDeltaTime);
        rb.linearVelocity += change;
        currentVelocity = rb.linearVelocity;

        //if (animator)
        //{
        //    animator.SetFloat("Speed", rb.linearVelocity.magnitude);
        //    if (rb.linearVelocity.sqrMagnitude > 0.0001f)
        //    {
        //        var dir = rb.linearVelocity.normalized;
        //        animator.SetFloat("MoveX", dir.x);
        //        animator.SetFloat("MoveY", dir.y);
        //    }
        //}

        // --- Melee whenever off cooldown; overlap will decide if it's actually in range ---
        if (Time.time >= lastAttackTime + meleeCooldown)
        {
            Vector2 aimDir = toTarget.sqrMagnitude > 0.0001f ? toTarget.normalized : Vector2.right;
            TryMelee(aimDir);
        }
    }

    // Choose closest ALIVE target (player vs spatula) with hysteresis to avoid jittery switching
    private void ChooseTarget()
    {
        Transform alivePlayer = (player && IsAlive(player)) ? player : null;
        Transform aliveSpatula = (spatula && IsAlive(spatula)) ? spatula : null;

        if (!alivePlayer && !aliveSpatula)
        {
            currentTarget = null;
            return;
        }
        if (alivePlayer && !aliveSpatula) { currentTarget = alivePlayer; return; }
        if (!alivePlayer && aliveSpatula) { currentTarget = aliveSpatula; return; }

        // Both alive: pick nearest, but only switch if it's significantly closer (hysteresis)
        float dPlayer = Vector2.Distance(transform.position, alivePlayer.position);
        float dSpatula = Vector2.Distance(transform.position, aliveSpatula.position);

        Transform best = dPlayer <= dSpatula ? alivePlayer : aliveSpatula;

        if (currentTarget == null)
        {
            currentTarget = best;
            return;
        }

        // Only switch if the new one is closer by at least 'switchHysteresis'
        float dCur = Vector2.Distance(transform.position, currentTarget.position);
        float dBest = Vector2.Distance(transform.position, best.position);

        if (dBest + switchHysteresis < dCur)
            currentTarget = best;
        // else keep current target to prevent rapid toggling
    }

    private bool IsAlive(Transform t)
    {
        if (!t) return false;
        var h = t.GetComponentInParent<Health>();
        return h != null && h.IsAlive;
    }

    private void TryMelee(Vector2 aimDir)
    {
        lastAttackTime = Time.time;
        //if (animator) animator.SetTrigger("Attack");

        Vector2 center = (Vector2)transform.position + aimDir * meleeReach;
        var hits = Physics2D.OverlapCircleAll(center, meleeRadius, meleeTargets);
        if (hits == null || hits.Length == 0) return;

        float halfArc = meleeArcDeg * 0.5f;
        foreach (var h in hits)
        {
            if (!h) continue;
            if (h.attachedRigidbody && h.attachedRigidbody.gameObject == gameObject) continue;

            // Arc filter
            Vector2 toTarget = ((Vector2)h.bounds.center - (Vector2)transform.position).normalized;
            float angle = Vector2.SignedAngle(aimDir, toTarget);
            if (Mathf.Abs(angle) > halfArc) continue;

            // Accept Player OR Spatula (both should implement IDamageable via Health)
            IDamageable dmg = h.GetComponent<IDamageable>();
            if (dmg == null) dmg = h.GetComponentInParent<IDamageable>();
            if (dmg == null || !dmg.IsAlive) continue;

            dmg.TakeDamage(meleeDamage, (Vector2)transform.position);
            break; // hit one target per swing
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying || !currentTarget) return;
        Vector2 aim = ((Vector2)currentTarget.position - (Vector2)transform.position).normalized;
        Vector2 c = (Vector2)transform.position + aim * meleeReach;
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(c, meleeRadius);
    }
#endif
}
