using System;
using System.Collections.Generic;
using UnityEngine;

public class PlayerInventory : MonoBehaviour
{
    [Serializable]
    public class WeaponInstance
    {
        public WeaponDefinition def;
        public int durability;

        public WeaponInstance(WeaponDefinition d)
        {
            def = d;
            durability = d ? Mathf.Max(1, d.maxDurability) : 1;
        }
        public bool IsBroken => durability <= 0;
    }

    [SerializeField] private int maxSlots = 8;
    public List<WeaponInstance> slots = new();
    public int equippedIndex = -1;

    public event Action OnInventoryChanged;
    public event Action<int> OnEquippedChanged; // passes new index

    public bool AddWeapon(WeaponDefinition def)
    {
        if (!def) return false;
        if (slots.Count >= maxSlots) return false;

        slots.Add(new WeaponInstance(def));
        OnInventoryChanged?.Invoke();

        // auto-equip if empty
        if (equippedIndex < 0) Equip(slots.Count - 1);
        return true;
    }

    public void Equip(int index)
    {
        if (index < 0 || index >= slots.Count) return;
        if (slots[index].IsBroken) return;

        equippedIndex = index;
        OnEquippedChanged?.Invoke(equippedIndex);
        OnInventoryChanged?.Invoke();
    }

    public WeaponInstance GetEquipped()
    {
        if (equippedIndex < 0 || equippedIndex >= slots.Count) return null;
        return slots[equippedIndex];
    }

    public void DamageEquippedDurability(int amount = 1)
    {
        var inst = GetEquipped();
        if (inst == null) return;

        inst.durability = Mathf.Max(0, inst.durability - Mathf.Abs(amount));

        if (inst.IsBroken)
        {
            // Break: unequip and optionally remove
            int brokeIndex = equippedIndex;
            equippedIndex = -1;
            OnEquippedChanged?.Invoke(-1);
            // Optional: keep broken in inventory? remove:
            slots.RemoveAt(brokeIndex);
            OnInventoryChanged?.Invoke();
        }
        else
        {
            OnInventoryChanged?.Invoke();
        }
    }
}
