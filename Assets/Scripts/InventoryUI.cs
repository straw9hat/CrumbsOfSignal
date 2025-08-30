using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class InventoryUI : MonoBehaviour
{
    [SerializeField] private PlayerInventory inventory;
    [SerializeField] private GameObject panel;          // the inventory Panel GameObject
    [SerializeField] private Transform gridRoot;        // content with GridLayoutGroup
    [SerializeField] private GameObject slotButtonPrefab;

    private void OnEnable()
    {
        if (inventory)
        {
            inventory.OnInventoryChanged += Rebuild;
            inventory.OnEquippedChanged += _ => Rebuild();
        }
        Rebuild();
    }

    private void OnDisable()
    {
        if (inventory)
        {
            inventory.OnInventoryChanged -= Rebuild;
            inventory.OnEquippedChanged -= _ => Rebuild();
        }
    }

    public void TogglePanel()
    {
        if (!panel) return;
        panel.SetActive(!panel.activeSelf);
        if (panel.activeSelf) Rebuild();
    }

    private void Clear()
    {
        for (int i = gridRoot.childCount - 1; i >= 0; i--)
            Destroy(gridRoot.GetChild(i).gameObject);
    }

    private void Rebuild()
    {
        if (!panel || !panel.activeSelf || !inventory) return;
        Clear();

        for (int i = 0; i < inventory.slots.Count; i++)
        {
            var inst = inventory.slots[i];
            var go = Instantiate(slotButtonPrefab, gridRoot);
            var btn = go.GetComponent<Button>();
            var img = go.GetComponentInChildren<Image>();
            var txt = go.GetComponentInChildren<TMP_Text>();

            if (img) img.sprite = inst.def ? inst.def.icon : null;
            if (txt) txt.text = inst.def
                ? $"{inst.def.displayName}\n{inst.durability}/{inst.def.maxDurability}"
                : "—";

            int index = i;
            btn.onClick.AddListener(() => inventory.Equip(index));

            // highlight equipped
            var colors = btn.colors;
            colors.normalColor = (index == inventory.equippedIndex) ? new Color(0.8f, 1f, 0.8f, 1f) : Color.white;
            btn.colors = colors;
        }
    }
}
