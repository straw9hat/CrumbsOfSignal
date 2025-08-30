using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(Health))]
public class PlayerController : MonoBehaviour
{
    [Header("Refs")]
    [SerializeField] private Transform graphicsRoot;     // child with SpriteRenderer/Animator
    [SerializeField] private Animator animator;          // optional

    [Header("Movement")]
    [SerializeField] private float walkSpeed = 3.5f;
    [SerializeField] private float sprintSpeed = 5.5f;
    [SerializeField] private float acceleration = 20f;
    [SerializeField] private float deceleration = 30f;

    [Header("Melee")]
    [SerializeField] private int meleeDamage = 20;
    [SerializeField] private float meleeCooldown = 0.35f;
    [SerializeField] private float meleeReach = 1.1f;   // distance from player center
    [SerializeField] private float meleeRadius = 0.75f; // hit circle radius
    [SerializeField] private float meleeArcDeg = 120f;  // optional: only hit within arc
    [SerializeField] private LayerMask meleeTargets;    // set to Enemy layer(s)

    [Header("Dodge")]
    [SerializeField] private float dodgeIFrameDuration = 0.5f;
    [SerializeField] private float postDodgeDelay = 0.1f;

    private Rigidbody2D rb;
    private Health health;
    private Vector2 moveInput;
    private bool sprintHeld;
    private Vector2 currentVelocity;
    private bool isDodging;
    private float nextDodgeAllowedTime = 0f;
    private Coroutine dodgeCo;

    [SerializeField] private PlayerInventory inventory;

    [SerializeField] private InventoryUI inventoryUI;

    // replace your static melee fields *or* keep them as fallback
    [SerializeField] private int fallbackDamage = 10;
    [SerializeField] private float fallbackCooldown = 0.35f;
    [SerializeField] private float fallbackReach = 1.0f;
    [SerializeField] private float fallbackRadius = 0.7f;
    [SerializeField] private float fallbackArcDeg = 120f;

    private float lastAttackTime = -999f;

    // Input System (generated C# from your input actions)
    private InputSystem_Actions controls;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<Health>();

        controls = new InputSystem_Actions();
        controls.Gameplay.Move.performed += OnMove;
        controls.Gameplay.Move.canceled += OnMove;
        controls.Gameplay.Sprint.performed += ctx => sprintHeld = ctx.ReadValueAsButton();
        controls.Gameplay.Sprint.canceled += ctx => sprintHeld = false;

        // NEW: Dodge action (bind this in your Input Actions to Space)
        controls.Gameplay.Dodge.performed += ctx => TryDodge();

        // Attack (unchanged, but we’ll cancel dodge inside TryMelee)
        controls.Gameplay.Attack.performed += ctx => TryMelee();

        //Inventory toggle
        controls.Gameplay.Inventory.performed += ctx => Toggle();
    }

    private void TryDodge()
    {
        if (Time.time < nextDodgeAllowedTime) return;
        if (isDodging) return;
        if (!health || !health.IsAlive) return;

        if (dodgeCo != null) StopCoroutine(dodgeCo);
        dodgeCo = StartCoroutine(DodgeRoutine());
    }

    private void Toggle()
    {
        if (inventoryUI != null)
            inventoryUI.TogglePanel();
    }

    private IEnumerator DodgeRoutine()
    {
        isDodging = true;

        // trigger i-frames
        health.TriggerIFrames(dodgeIFrameDuration);

        // (optional) play a dodge anim
        if (animator) animator.SetTrigger("Dodge");

        // you can also tweak movement here (e.g., burst/roll), but not required
        yield return new WaitForSeconds(dodgeIFrameDuration);

        isDodging = false;
        nextDodgeAllowedTime = Time.time + postDodgeDelay;
        dodgeCo = null;
    }

    private void OnEnable() => controls.Gameplay.Enable();
    private void OnDisable() => controls.Gameplay.Disable();

    private void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
    }

    private void FixedUpdate()
    {
        if (!health.IsAlive) { rb.linearVelocity = Vector2.zero; return; }

        Vector2 desiredDir = moveInput.sqrMagnitude > 1e-4f ? moveInput.normalized : Vector2.zero;
        float targetSpeed = sprintHeld ? sprintSpeed : walkSpeed;
        Vector2 desiredVel = desiredDir * targetSpeed;

        Vector2 vel = rb.linearVelocity;
        Vector2 diff = desiredVel - vel;
        float rate = (desiredDir == Vector2.zero) ? deceleration : acceleration;
        Vector2 change = Vector2.ClampMagnitude(diff, rate * Time.fixedDeltaTime);

        rb.linearVelocity = vel + change;
        currentVelocity = rb.linearVelocity;

        // Anim + facing
        if (animator != null)
        {
            animator.SetFloat("Speed", currentVelocity.magnitude);
            if (currentVelocity.sqrMagnitude > 0.0001f)
            {
                var dir = currentVelocity.normalized;
                animator.SetFloat("MoveX", dir.x);
                animator.SetFloat("MoveY", dir.y);
            }
        }

        // Flip graphics based on aim or movement
        Vector2 aimDir = GetAimDirection();
        if (graphicsRoot != null)
        {
            float faceX = Mathf.Abs(graphicsRoot.localScale.x) * (aimDir.x < 0 ? -1 : 1);
            var s = graphicsRoot.localScale;
            s.x = Mathf.Approximately(aimDir.x, 0f) ? s.x : faceX;
            graphicsRoot.localScale = s;
        }
    }

    private Vector2 GetAimDirection()
    {
        // Mouse world position (New Input System)
        Vector2 mousePos = Mouse.current != null
            ? (Vector2)Camera.main.ScreenToWorldPoint(Mouse.current.position.ReadValue())
            : (Vector2)(transform.position + (Vector3)moveInput); // fallback to move dir

        Vector2 dir = mousePos - (Vector2)transform.position;
        if (dir.sqrMagnitude < 1e-6f) dir = currentVelocity.sqrMagnitude > 0 ? currentVelocity.normalized : Vector2.right;
        return dir.normalized;
    }

    private bool GetEquippedParams(out int dmg, out float cd, out float reach, out float radius, out float arc)
    {
        dmg = fallbackDamage; cd = fallbackCooldown;
        reach = fallbackReach; radius = fallbackRadius; arc = fallbackArcDeg;

        var inst = inventory ? inventory.GetEquipped() : null;
        if (inst == null || inst.def == null) return false;

        dmg = inst.def.baseDamage;
        cd = inst.def.cooldown;
        reach = inst.def.reach;
        radius = inst.def.radius;
        arc = inst.def.arcDegrees;
        return true;
    }

    private void TryMelee()
    {
        if (!health || !health.IsAlive) return;

        GetEquippedParams(out int dmg, out float cd, out float reach, out float radius, out float arc);

        if (Time.time < lastAttackTime + cd) return;
        lastAttackTime = Time.time;

        Vector2 aimDir = GetAimDirection();
        Vector2 center = (Vector2)transform.position + aimDir * reach;

        if (animator != null) animator.SetTrigger("Attack");

        var hits = Physics2D.OverlapCircleAll(center, radius, meleeTargets);
        if (hits == null || hits.Length == 0) return;

        bool hitSomething = false;
        float halfArc = arc * 0.5f;
        foreach (var h in hits)
        {
            if (h.attachedRigidbody && h.attachedRigidbody.gameObject == gameObject) continue;

            Vector2 toTarget = ((Vector2)h.transform.position - (Vector2)transform.position).normalized;
            float ang = Vector2.SignedAngle(aimDir, toTarget);
            if (Mathf.Abs(ang) > halfArc) continue;

            if (h.TryGetComponent<IDamageable>(out var target) && target.IsAlive)
            {
                target.TakeDamage(dmg, transform.position);
                hitSomething = true;
            }
        }

        // Only consume durability on successful hits
        if (hitSomething && inventory) inventory.DamageEquippedDurability(1);
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        // visualize attack area toward current aim
        Vector2 aim = Application.isPlaying ? GetAimDirection() : Vector2.right;
        Vector2 c = (Vector2)transform.position + aim * meleeReach;
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(c, meleeRadius);
    }
#endif
}
