using UnityEngine;

/// <summary>
/// Anything the player can hit. Buildings implement it now, enemies in Stage 4,
/// so the attack code never needs to know what it just hit.
/// </summary>
public abstract class Damageable : MonoBehaviour
{
    public abstract bool IsAlive { get; }
    public abstract void TakeDamage(float amount, Vector2 from);
}
