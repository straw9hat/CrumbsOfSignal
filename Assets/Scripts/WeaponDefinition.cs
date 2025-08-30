using UnityEngine;

[CreateAssetMenu(menuName = "Game/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    public string displayName = "Sword";
    public Sprite icon;
    [Header("Combat")]
    public int baseDamage = 20;
    public float reach = 1.1f;
    public float radius = 0.75f;
    [Range(30f, 360f)] public float arcDegrees = 120f;
    public float cooldown = 0.35f;
    [Header("Durability")]
    public int maxDurability = 10;   // number of *successful* hits before breaking
}
