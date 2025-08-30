using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class WeaponPickup : MonoBehaviour
{
    public WeaponDefinition weapon;
    public bool destroyOnPickup = true;

    private void Reset()
    {
        var col = GetComponent<Collider2D>();
        col.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var inv = other.GetComponentInParent<PlayerInventory>();
        if (!inv || !weapon) return;

        if (inv.AddWeapon(weapon))
        {
            // TODO: pickup VFX/SFX
            if (destroyOnPickup) Destroy(gameObject);
        }
    }
}
