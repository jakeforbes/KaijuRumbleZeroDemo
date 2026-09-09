using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chases the player, stops at its attack range, hits on a cooldown. Dies to swipes,
/// or to being walked over once the kaiju outgrows it.
/// </summary>
public class Enemy : Damageable
{
    public static readonly List<Enemy> All = new();

    static Transform root;

    public Tuning tuning;
    public EnemyType type;

    public override bool IsAlive => hp > 0f;

    float hp;
    float nextAttackAt;
    float flashUntil;

    Rigidbody2D body;
    SpriteRenderer sr;
    Color baseColour;

    public static Enemy Spawn(Tuning tuning, EnemyType type, Vector3 at, float ppu)
    {
        if (root == null) root = new GameObject("Enemies").transform;

        var go = new GameObject(type.name);
        go.transform.SetParent(root, false);
        go.transform.position = at;

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        var col = go.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        float w = type.bodyPx / ppu * 0.8f;
        col.size = new Vector2(w, w * 0.5f);

        var art = new GameObject("Art").transform;
        art.SetParent(go.transform, false);

        var shadowGo = new GameObject("Shadow");
        shadowGo.transform.SetParent(art, false);
        var ssr = shadowGo.AddComponent<SpriteRenderer>();
        ssr.sprite = GreyboxArt.Shadow(Mathf.Max(24, type.bodyPx), ppu);
        ssr.sortingOrder = -1;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(art, false);
        var bsr = bodyGo.AddComponent<SpriteRenderer>();
        bsr.sprite = GreyboxArt.Capsule(Mathf.RoundToInt(type.bodyPx * 0.7f), type.bodyPx,
                                        type.colour, ppu);

        var e = go.AddComponent<Enemy>();
        e.tuning = tuning;
        e.type = type;
        e.hp = type.hp;
        e.body = rb;
        e.sr = bsr;
        e.baseColour = type.colour;

        AudioEvents.Play(Sfx.EnemySpawn, at, 0.3f);
        return e;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null || progress.IsDead) { body.linearVelocity = Vector2.zero; return; }

        Vector2 toPlayer = progress.transform.position - transform.position;
        float flat = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).magnitude;

        // Outgrown enemies die underfoot. No input, no damage taken — this is the
        // whole reward for having grown.
        if (progress.Tier > type.sizeClass && flat <= tuning.squishRange * progress.Scale)
        {
            Squish();
            return;
        }

        if (flat > type.attackRange)
        {
            Vector2 dir = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).normalized;
            body.linearVelocity = new Vector2(dir.x, dir.y * tuning.isoSquash) * type.moveSpeed;
        }
        else
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= nextAttackAt)
            {
                nextAttackAt = Time.time + type.attackCooldown;
                progress.TakeDamage(type.contactDamage);
            }
        }

        if (Time.time < flashUntil) sr.color = Color.white;
        else sr.color = baseColour;
    }

    public override void TakeDamage(float amount, Vector2 from)
    {
        if (!IsAlive) return;

        // Armour is flat subtraction, so chip damage genuinely bounces off heavies.
        float dealt = Mathf.Max(0f, amount - type.armour);
        if (dealt <= 0f) { flashUntil = Time.time + 0.06f; return; }

        hp -= dealt;
        flashUntil = Time.time + 0.08f;

        if (hp <= 0f) Die(Sfx.EnemyDeath);
    }

    void Squish()
    {
        AudioEvents.Play(Sfx.Squish, transform.position, 0.7f);
        Die(Sfx.Squish);
    }

    void Die(Sfx sound)
    {
        hp = 0f;
        if (sound == Sfx.EnemyDeath) AudioEvents.Play(Sfx.EnemyDeath, transform.position, 0.5f);
        Food.Scatter(tuning, transform.position, type.foodDrops, type.foodScatter, tuning.pixelsPerUnit);
        Destroy(gameObject);
    }

    /// <summary>Knocked back by the shockwave when the kaiju shrinks.</summary>
    public void Push(Vector2 fromPoint, float force)
    {
        Vector2 away = (Vector2)transform.position - fromPoint;
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle;
        body.linearVelocity = away.normalized * force;
    }

    public static void KillAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) Destroy(All[i].gameObject);
    }

    public static void Reset() => root = null;
}
