using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Toxin upgrade: a cloud around the kaiju that also stays where it has been.
///
/// Puffs are dropped as the player moves and each lingers, so the aura and the trail
/// are the same mechanism — standing still gives you a cloud, running gives you a
/// wake. Nothing has to switch between the two.
///
/// Damage is settled here rather than on each puff, and that is the whole reason this
/// class exists. Eight overlapping puffs each ticking for themselves would deal eight
/// times the damage to anything standing in the trail, which is both wrong and
/// invisible until someone wonders why the poison melts bosses. One tick asks whether
/// an enemy is inside any puff at all, and charges it once.
/// </summary>
public class ToxicField : MonoBehaviour
{
    struct Puff
    {
        public Vector2 at;
        public float radius;
        public float bornAt;
        public float diesAt;
        public SpriteRenderer art;
    }

    public Tuning tuning;

    static Sprite sprite;
    static Transform root;

    readonly List<Puff> puffs = new();
    float nextEmitAt;
    float nextTickAt;

    public static void Reset() => root = null;

    /// <summary>Drops a puff at a point, sized for the kaiju standing there.</summary>
    public void Emit(Vector2 at, float radius)
    {
        // Life is fixed when the puff is laid down, not read every frame, so levelling
        // Toxin stretches the wake from here on instead of reaching back and extending
        // gas that was already dispersing.
        var upgrades = PlayerUpgrades.Instance;
        float life = upgrades != null ? upgrades.ToxinCloudLife : tuning.toxinCloudLife;

        if (root == null) root = new GameObject("Toxin").transform;
        if (sprite == null) sprite = GreyboxArt.Cloud(128, tuning.pixelsPerUnit);

        var go = new GameObject("puff");
        go.transform.SetParent(root, false);
        go.transform.position = at;

        // Cloud is white, so the tint here decides the colour. It is a 2:1 ellipse,
        // which lies on the ground plane the same way the damage check measures.
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = new Color(0.45f, 0.85f, 0.30f, 0.30f);
        sr.sortingOrder = -40;   // above the ground tiles, under everything that sorts by Y
        go.transform.localScale = Vector3.one * (radius * 2f * tuning.pixelsPerUnit / 128f);

        puffs.Add(new Puff
        {
            at = at,
            radius = radius,
            bornAt = Time.time,
            diesAt = Time.time + life,
            art = sr,
        });
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        var upgrades = PlayerUpgrades.Instance;
        if (progress == null || upgrades == null) return;

        Fade();

        if (!upgrades.HasToxin || progress.RunOver) return;

        // Radius grows with the square root of size, not with it. A radius that
        // tracked size directly would quadruple the area by size 5 — the same trap
        // the camera zoom, the food magnet and the stomp all fell into.
        float radius = tuning.toxinRadius * Mathf.Pow(progress.Scale, tuning.toxinRadiusExponent);

        if (Time.time >= nextEmitAt)
        {
            nextEmitAt = Time.time + tuning.toxinEmitInterval;
            Emit(progress.transform.position, radius);
        }

        if (Time.time < nextTickAt) return;
        nextTickAt = Time.time + tuning.toxinTickInterval;
        Tick(progress, upgrades);
    }

    void Tick(PlayerProgress progress, PlayerUpgrades upgrades)
    {
        float damage = tuning.toxinDamage * upgrades.ToxinPowerMul * progress.DamageMultiplier;
        if (damage <= 0f) return;

        // Backwards, because a kill removes the enemy from the list mid-loop.
        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            var e = Enemy.All[i];
            if (e == null || !e.IsAlive) continue;

            // Ignores armour. A tick is a fraction of any heavy's armour value, so a
            // flat subtraction wipes it out completely — the cloud would be lethal to
            // grunts and literally nothing to a Tank, a Mech or the boss, which is
            // backwards for an upgrade whose whole shape is attrition on whatever is
            // still following you late in a run.
            if (Inside(e.transform.position)) e.TakeDamage(damage, e.transform.position, true);
        }
    }

    bool Inside(Vector2 point)
    {
        for (int i = 0; i < puffs.Count; i++)
        {
            Vector2 d = point - puffs[i].at;
            if (new Vector2(d.x, d.y / tuning.isoSquash).magnitude <= puffs[i].radius) return true;
        }
        return false;
    }

    /// <summary>Thins out over its life, so the trail reads as dispersing rather than blinking off.</summary>
    void Fade()
    {
        for (int i = puffs.Count - 1; i >= 0; i--)
        {
            var p = puffs[i];
            if (Time.time >= p.diesAt || p.art == null)
            {
                if (p.art != null) Destroy(p.art.gameObject);
                puffs.RemoveAt(i);
                continue;
            }

            float life = Mathf.InverseLerp(p.bornAt, p.diesAt, Time.time);
            var c = p.art.color;
            c.a = Mathf.Lerp(0.30f, 0f, life * life);
            p.art.color = c;

            // Spreading slightly as it thins is most of what sells it as gas.
            p.art.transform.localScale = Vector3.one
                * (p.radius * 2f * tuning.pixelsPerUnit / 128f) * Mathf.Lerp(0.8f, 1.25f, life);
        }
    }
}
