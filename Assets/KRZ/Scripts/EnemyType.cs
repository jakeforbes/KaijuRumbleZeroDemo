using UnityEngine;

/// <summary>
/// One enemy species. sizeClass drives squishing: once your size exceeds it, you
/// kill this thing by walking over it. That threshold is the payoff for growing —
/// the enemy that was a threat two minutes ago becoming scenery.
/// </summary>
[System.Serializable]
public class EnemyType
{
    public string name = "Grunt";

    [Tooltip("Squishable once the kaiju's size is above this. 0 is size 1.")]
    [Range(0, 4)] public int sizeClass;

    public float hp = 12f;

    [Tooltip("Flat damage subtracted from every hit taken. Makes auto-swipe feel " +
             "useless against heavies and gives the special a job.")]
    public float armour;

    public float contactDamage = 6f;
    public float moveSpeed = 3.4f;

    [Tooltip("How close it gets before it stops and attacks.")]
    public float attackRange = 0.9f;

    [Tooltip("Fires a tracer from where it stands instead of lunging. Set this rather " +
             "than relying on attackRange, so retuning a range cannot silently turn a " +
             "ranged attacker into a melee one.")]
    public bool ranged;
    public float attackCooldown = 1.1f;

    [Tooltip("Telegraph before the hit lands. Damage arriving with no warning reads as " +
             "unfair and cannot be tuned, because you never see what hit you.")]
    public float attackWindup = 0.35f;

    public int foodDrops = 2;
    public float foodScatter = 1.2f;

    [Tooltip("Leaves a power-up on death. Elites only — it is the reason to fight one " +
             "rather than walk away from it.")]
    public bool dropsUpgrade;

    [Header("Missile volley")]
    [Tooltip("Periodically stops, telegraphs, then fires a cluster of homing missiles.")]
    public bool volley;

    public float volleyCooldown = 5f;

    [Tooltip("How long it stands still before firing. This is the tell — long enough " +
             "to see it coming and break line of sight or close the distance.")]
    public float volleyWindup = 1f;

    [Tooltip("Will not fire from further than this, so off-screen enemies stay quiet.")]
    public float volleyRange = 18f;

    public int volleyCount = 8;
    public float missileDamage = 12f;
    public float missileSpeed = 14f;

    [Tooltip("How hard a missile can change direction. Low turns wide and is dodgeable; " +
             "high tracks you around corners.")]
    public float missileTurn = 40f;

    public float missileLife = 4f;

    [Tooltip("Body height in pixels at 128 PPU. 64 is roughly half a size-1 kaiju.")]
    public int bodyPx = 64;

    public Color colour = new Color(0.85f, 0.45f, 0.35f);
}
