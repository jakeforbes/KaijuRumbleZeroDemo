using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chases the player, stops at its attack range, hits on a cooldown. Dies to swipes,
/// or to being walked over once the kaiju outgrows it.
/// </summary>
[SoundActions(Sfx.EnemySpawn, Sfx.Footstep, Sfx.EnemyAttack, Sfx.EnemyHit, Sfx.EnemyBlocked, Sfx.EnemyDeath, Sfx.Squish, Sfx.EnemyPushed)]
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
    float windEndsAt;
    bool winding;
    float nextVolleyAt;
    float volleyEndsAt;
    bool volleyWinding;

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

        e.nextVolleyAt = Time.time + type.specialCooldown;

        SoundPlayer.Attach(go, type.sounds != null ? type.sounds : tuning.enemySounds);
        AudioEvents.Play(Sfx.EnemySpawn, at, 0.3f, go);
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
        // whole reward for having grown. Thresholds are spaced so each class stays
        // a real threat for several sizes rather than going trivial immediately.
        int squishAt = tuning.squishFirstTier + type.sizeClass * tuning.squishTiersPerClass;
        if (progress.Tier >= squishAt && flat <= tuning.squishRange * progress.Scale)
        {
            Squish();
            return;
        }

        // The volley takes priority over everything: it plants the Mech, telegraphs,
        // then fires. Handled before movement so the stop is absolute rather than a
        // Mech that keeps walking while it winds up.
        if (type.special != SpecialAction.None && HandleSpecial(progress, flat)) return;

        if (flat > type.attackRange)
        {
            winding = false;
            Vector2 dir = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).normalized;
            body.linearVelocity = new Vector2(dir.x, dir.y * tuning.isoSquash) * type.moveSpeed;
        }
        else
        {
            body.linearVelocity = Vector2.zero;

            // A Dropship holds station at its range and never strikes — its whole
            // threat is what it unloads, so ignoring it costs you the swarm, not health.
            if (!type.attacks) { Recolour(); return; }

            if (!winding && Time.time >= nextAttackAt)
            {
                winding = true;
                windEndsAt = Time.time + type.attackWindup;
            }

            if (winding && Time.time >= windEndsAt)
            {
                winding = false;
                nextAttackAt = Time.time + type.attackCooldown;
                AudioEvents.Play(Sfx.EnemyAttack, transform.position, owner: gameObject);
                Strike(progress);
            }
        }

        Recolour();
    }

    /// <summary>Returns true while the special owns this frame.</summary>
    bool HandleSpecial(PlayerProgress progress, float flat)
    {
        if (volleyWinding)
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= volleyEndsAt)
            {
                volleyWinding = false;
                nextVolleyAt = Time.time + type.specialCooldown;
                FireSpecial();
            }
            Recolour();
            return true;
        }

        if (Time.time >= nextVolleyAt && flat <= type.specialRange && CanFireSpecial())
        {
            volleyWinding = true;
            volleyEndsAt = Time.time + type.specialWindup;
            body.linearVelocity = Vector2.zero;
            Recolour();
            return true;
        }

        return false;
    }

    /// <summary>Lets a special decline to start, rather than telegraphing and doing nothing.</summary>
    bool CanFireSpecial()
    {
        if (type.special != SpecialAction.DeployTroops) return true;

        int mine = 0;
        foreach (var e in All)
            if (e != null && e.type != null && e.type.name == type.deployType) mine++;
        return mine < type.deployMaxAlive;
    }

    void FireSpecial()
    {
        switch (type.special)
        {
            case SpecialAction.MissileVolley:
                Missile.Volley(tuning, type, transform.position, tuning.pixelsPerUnit);
                break;

            case SpecialAction.DeployTroops:
                Deploy();
                break;
        }
    }

    void Deploy()
    {
        var spawnType = GameBootstrap.Instance != null
            ? GameBootstrap.Instance.FindType(type.deployType)
            : null;
        if (spawnType == null) return;

        AudioEvents.Play(Sfx.EnemyDeploy, transform.position, owner: gameObject);

        for (int i = 0; i < type.deployCount; i++)
        {
            float angle = i / (float)type.deployCount * Mathf.PI * 2f + Random.value;
            var at = transform.position + new Vector3(
                Mathf.Cos(angle) * type.deploySpread,
                Mathf.Sin(angle) * type.deploySpread * tuning.isoSquash, 0f);

            // Skip blocked ground rather than dropping troops inside a building,
            // where the solver would fling them across the map.
            if (Physics2D.OverlapCircle(at, 0.4f) != null) continue;

            Enemy.Spawn(tuning, spawnType, at, tuning.pixelsPerUnit);
        }
    }

    void Recolour()
    {
        if (Time.time < flashUntil) { sr.color = Color.white; return; }

        if (volleyWinding)
        {
            // A faster, hotter pulse than the melee tell, so the two read differently.
            float t = 1f - Mathf.Clamp01((volleyEndsAt - Time.time) / Mathf.Max(0.01f, type.specialWindup));
            sr.color = Color.Lerp(baseColour, new Color(1f, 0.45f, 0.2f),
                                  Mathf.PingPong(t * 6f, 1f) * 0.5f + t * 0.5f);
            return;
        }

        if (winding)
        {
            // Pulses harder as the strike approaches, so the timing is readable.
            float t = 1f - Mathf.Clamp01((windEndsAt - Time.time) / Mathf.Max(0.01f, type.attackWindup));
            sr.color = Color.Lerp(baseColour, new Color(1f, 0.95f, 0.85f), t * t);
            return;
        }

        sr.color = baseColour;
    }

    void Strike(PlayerProgress progress)
    {
        Vector3 target = progress.transform.position;

        if (type.ranged)
            HitFx.Line(transform.position + Vector3.up * (type.bodyPx * 0.6f / tuning.pixelsPerUnit),
                       target, new Color(1f, 0.7f, 0.35f), tuning.pixelsPerUnit, 0.16f, 0.18f);
        else
            body.linearVelocity = ((Vector2)(target - transform.position)).normalized * 6f;

        HitFx.Burst(target, new Color(1f, 0.45f, 0.35f), 0.9f * progress.Scale, tuning.pixelsPerUnit);
        progress.TakeDamage(type.contactDamage);
    }

    public override void TakeDamage(float amount, Vector2 from)
    {
        if (!IsAlive) return;

        // Armour is flat subtraction, so chip damage genuinely bounces off heavies.
        float dealt = Mathf.Max(0f, amount - type.armour);

        if (dealt <= 0f)
        {
            // Says "your weapon is wrong" rather than looking like a missed hit.
            flashUntil = Time.time + 0.06f;
            AudioEvents.Play(Sfx.EnemyBlocked, transform.position, owner: gameObject);
            Popups.Add(transform.position, "<b>BLOCKED</b>", new Color(0.65f, 0.7f, 0.8f));
            return;
        }

        hp -= dealt;
        AudioEvents.Play(Sfx.EnemyHit, transform.position, owner: gameObject);
        flashUntil = Time.time + 0.08f;
        Popups.Add(transform.position, $"{dealt:0}", Color.white);

        if (hp <= 0f) Die(Sfx.EnemyDeath);
    }

    void Squish()
    {
        AudioEvents.Play(Sfx.Squish, transform.position, 0.7f, owner: gameObject);
        Die(Sfx.Squish);
    }

    void Die(Sfx sound)
    {
        hp = 0f;
        if (sound == Sfx.EnemyDeath) AudioEvents.Play(Sfx.EnemyDeath, transform.position, 0.5f, owner: gameObject);
        Food.Scatter(tuning, transform.position, type.foodDrops, type.foodScatter, tuning.pixelsPerUnit);

        if (type.dropsUpgrade && PlayerUpgrades.Instance != null)
            UpgradePickup.Spawn(tuning, PlayerUpgrades.Instance.RollDrop(),
                                transform.position, tuning.pixelsPerUnit);

        Destroy(gameObject);
    }

    /// <summary>Knocked back by the shockwave when the kaiju shrinks.</summary>
    public void Push(Vector2 fromPoint, float force)
    {
        Vector2 away = (Vector2)transform.position - fromPoint;
        if (away.sqrMagnitude < 0.001f) away = Random.insideUnitCircle;
        body.linearVelocity = away.normalized * force;
        AudioEvents.Play(Sfx.EnemyPushed, transform.position, owner: gameObject);
    }

    public static void KillAll()
    {
        for (int i = All.Count - 1; i >= 0; i--)
            if (All[i] != null) Destroy(All[i].gameObject);
    }

    public static void Reset() => root = null;
}
