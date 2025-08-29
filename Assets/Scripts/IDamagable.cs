using UnityEngine;

public interface IDamageable
{
    void TakeDamage(int amount, Vector2 hitFrom);
    bool IsAlive { get; }
}