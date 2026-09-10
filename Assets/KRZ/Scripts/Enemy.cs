using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Chases the player, stops at its attack range, hits on a cooldown. Dies to swipes,
/// or to being walked over once the kaiju outgrows it.
/// </summary>
[SoundActions(Sfx.EnemySpawn, Sfx.Footstep, Sfx.EnemyAttack, Sfx.EnemyHit, Sfx.EnemyBlocked, Sfx.EnemyDeath, Sfx.Squish, Sfx.EnemyPushed, Sfx.EnemyDeploy, Sfx.BossRoar, Sfx.MechPunch)]
public class Enemy : Damageable
{
    public static readonly List<Enemy> All = new();

    static Transform root;

    public Tuning tuning;
    public EnemyType type;

    public override bool IsAlive => hp > 0f;
    public bool IsBoss => type != null && (type.isBoss || type.name == "Abomination");

    float hp;
    float nextAttackAt;
    float flashUntil;
    float windEndsAt;
    bool winding;
    float nextVolleyAt;
    float volleyEndsAt;
    bool volleyWinding;
    bool dying;
    float destroyAt;
    HealthBar bar;
    float wanderAngle;

    public EnemyArt art;
    public Vector2 Velocity => body != null ? body.linearVelocity : Vector2.zero;

    Rigidbody2D body;
    SpriteRenderer sr;
    Color baseColour;

    readonly RaycastHit2D[] obstacleProbe = new RaycastHit2D[8];

    public static Enemy Spawn(Tuning tuning, EnemyType type, Vector3 at, float ppu)
    {
        if ((type.isBoss || type.name == "Abomination") && GameBootstrap.Instance != null
            && !GameBootstrap.Instance.TryFindBossSpawn(type, out at))
        {
            Debug.LogWarning("KRZ: no clear boss spawn at the playfield edge.");
            return null;
        }
        if (root == null) root = new GameObject("Enemies").transform;

        var go = new GameObject(type.name);
        go.transform.SetParent(root, false);
        go.transform.position = at;
        // Sort the entire enemy (including its health bar) from its ground position.
        // Child sprite centres and UI sorting orders must not put it above a building.
        go.AddComponent<UnityEngine.Rendering.SortingGroup>();

        var rb = go.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.freezeRotation = true;

        // Mass by body size. The kaiju's mass scales with its own growth, so infantry
        // are brushed aside while a Mech still has real presence at size 1 — and by
        // size 5 nothing short of the boss can move you. Anything that has to hold its
        // ground against a full-grown kaiju states its own mass instead.
        rb.mass = type.mass > 0f ? type.mass : Mathf.Max(0.2f, type.bodyPx / 64f);

        var col = go.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        float w = type.bodyPx / ppu * type.footprintFraction;
        col.size = new Vector2(w, w * 0.5f);

        // Anything too big for the streets walks through them instead. Done as a
        // per-pair ignore rather than a layer, because layers would have to be set up
        // in project settings and nothing else here needs the Editor touched.
        if (type.ignoresBuildings)
            foreach (var building in Building.All)
            {
                if (building == null) continue;
                foreach (var bc in building.GetComponents<Collider2D>())
                    if (bc != null) Physics2D.IgnoreCollision(col, bc);
            }

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

        // Delivered art replaces the greybox capsule when the type points at a folder.
        var art2 = go.AddComponent<EnemyArt>();
        if (art2.Init(e, bsr, type))
        {
            e.art = art2;

            // Tint rather than replace: one delivered model can serve several roles
            // at different sizes and colours, which is how Grunt, Trooper, Commander
            // and Dropship all come from a single flying tank.
            e.baseColour = type.artTint;
            bsr.color = type.artTint;

            // Scale is relative to the package's own frame size, so a 192px delivery
            // and a 512px one sit side by side at their intended on-screen heights.
            float frameSize = Mathf.Max(1, type.artFrameSize);
            bodyGo.transform.localScale = Vector3.one * (type.artDisplayPx / frameSize);
        }

        // Sized off the drawn sprite rather than bodyPx, so arcs cover the figure the
        // player can see instead of the collider they cannot.
        if (type.electrified)
        {
            float drawn = (e.art != null ? type.artDisplayPx : type.bodyPx) / ppu;

            // Arcs take the unit's own tint pushed toward white, so an electrified
            // enemy reads as that enemy overcharged rather than as a generic effect.
            var arc = Color.Lerp(e.baseColour, Color.white, 0.6f);
            ElectricFx.Attach(go, drawn, drawn * 0.30f, ppu, arc);
        }

        e.nextVolleyAt = Time.time + type.specialCooldown;
        e.wanderAngle = Random.value * Mathf.PI * 2f;

        var sounds = type.sounds;
        if (sounds == null) sounds = Resources.Load<SoundPlayer>("Enemy Sounds/" + type.name);
        SoundPlayer.Attach(go, sounds != null ? sounds : tuning.enemySounds);
        AudioEvents.Play(Sfx.EnemySpawn, at, 0.3f, go);
        if (e.IsBoss && Camera.main != null && Camera.main.TryGetComponent<CameraRig>(out var rig))
            rig.IntroduceBoss(e);
        return e;
    }

    void OnEnable() => All.Add(this);
    void OnDisable() => All.Remove(this);


    void Update()
    {
        // Corpses keep existing until their destruction clip finishes, so a kill reads
        // as an event rather than the object blinking out. No AI while dying.
        if (dying)
        {
            body.linearVelocity = Vector2.zero;
            if (Time.time >= destroyAt) Destroy(gameObject);
            return;
        }

        var progress = PlayerProgress.Instance;
        if (progress == null || progress.RunOver) { body.linearVelocity = Vector2.zero; return; }

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

        if (type.movement == MovementMode.Flee)
        {
            Flee(toPlayer, flat);
            if (bar != null && Squishable) { Destroy(bar.gameObject); bar = null; }
            Recolour();
            return;
        }

        // Ranges are measured from the kaiju's edge, not its centre. Measuring to the
        // centre meant an enemy had to bulldoze its way through the player's footprint
        // before it would stop pressing — which is what shoved the player around, and
        // why it got worse the bigger the kaiju grew.
        float playerRadius = tuning.playerFootprintFraction.x * 0.5f * progress.Scale;
        float stopAt = type.attackRange + playerRadius;

        if (flat > stopAt)
        {
            winding = false;
            Vector2 dir = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).normalized;
            Vector2 moveDir = new Vector2(dir.x, dir.y * tuning.isoSquash).normalized;
            body.linearVelocity = AvoidBuildings(moveDir) * type.moveSpeed;
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
                if (art != null) art.Play(EnemyArt.Attack, true);
                Strike(progress);
            }
        }

        if (bar != null && Squishable) { Destroy(bar.gameObject); bar = null; }
        Recolour();
    }

    /// <summary>
    /// Drifts on a slowly wandering heading, and commits to running directly away the
    /// closer the kaiju gets. Blending the two rather than switching between them is
    /// what makes it read as a skittish animal instead of a unit retreating in a line.
    /// </summary>
    void Flee(Vector2 toPlayer, float flat)
    {
        wanderAngle += Random.Range(-1f, 1f) * type.wanderRate * Time.deltaTime;
        var drift = new Vector2(Mathf.Cos(wanderAngle), Mathf.Sin(wanderAngle));

        Vector2 away = flat > 0.01f
            ? -new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).normalized
            : drift;

        // Panic rises from nothing at the edge of its notice to full at contact.
        float panic = Mathf.Clamp01(1f - flat / Mathf.Max(0.01f, type.fleeRadius));
        var dir = Vector2.Lerp(drift, away, panic).normalized;

        // Fleeing runs through the same obstacle avoidance as chasing, or a panicking
        // Scavenger would bolt straight into the first wall behind it.
        Vector2 moveDir = new Vector2(dir.x, dir.y * tuning.isoSquash).normalized;
        body.linearVelocity = AvoidBuildings(moveDir) * type.moveSpeed;
    }

    /// <summary>
    /// Rudimentary obstacle awareness. A single wall-slide isn't enough near a corner
    /// or a gap between two buildings — the slide direction can point straight into the
    /// second building, so it fans outward from the desired heading, left and right in
    /// alternation, and takes the first heading that's actually clear. Recomputed every
    /// frame, so as soon as a clearer line to the player opens up it takes it.
    /// </summary>
    Vector2 AvoidBuildings(Vector2 moveDir)
    {
        // Nothing to steer around when nothing blocks you, and the probe would find
        // buildings everywhere and fan uselessly.
        if (type.ignoresBuildings) return moveDir;

        float probeDist = Mathf.Max(1f, type.moveSpeed * 0.5f);
        if (IsHeadingClear(moveDir, probeDist)) return moveDir;

        for (int i = 1; i <= 6; i++)
        {
            float angle = i * 30f;
            Vector2 toLeft = Rotate(moveDir, angle);
            if (IsHeadingClear(toLeft, probeDist)) return toLeft;

            Vector2 toRight = Rotate(moveDir, -angle);
            if (IsHeadingClear(toRight, probeDist)) return toRight;
        }

        // Boxed in on every side. Standing still beats vibrating in place.
        return Vector2.zero;
    }

    bool IsHeadingClear(Vector2 dir, float dist)
    {
        int count = body.Cast(dir, obstacleProbe, dist);
        for (int i = 0; i < count; i++)
        {
            var hit = obstacleProbe[i];
            if (hit.collider.isTrigger) continue;
            if (hit.collider.GetComponent<Building>() == null) continue;
            return false;
        }
        return true;
    }

    static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad), sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    /// <summary>Whether the kaiju has outgrown this enemy's class.</summary>
    public bool Squishable
    {
        get
        {
            var p = PlayerProgress.Instance;
            if (p == null) return false;
            return p.Tier >= tuning.squishFirstTier + type.sizeClass * tuning.squishTiersPerClass;
        }
    }

    /// <summary>
    /// Appears on first damage, like a building's. Removed once the kaiju has outgrown
    /// the class: something you kill by walking over is not worth tracking, and sixty
    /// bars over things that die on contact is noise rather than feedback.
    /// </summary>
    void ShowBar()
    {
        if (!tuning.showEnemyHealthBars || Squishable)
        {
            if (bar != null) { Destroy(bar.gameObject); bar = null; }
            return;
        }

        float displayPx = art != null ? type.artDisplayPx : type.bodyPx;
        float top = displayPx / tuning.pixelsPerUnit;

        if (bar == null)
            bar = HealthBar.Attach(transform, top * 0.55f, top, tuning.pixelsPerUnit);

        bar.Set(hp / type.hp);
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
                AudioEvents.Play(Sfx.EnemyAttack, transform.position, owner: gameObject);
                Missile.Volley(tuning, type, transform.position, tuning.pixelsPerUnit);
                break;

            case SpecialAction.DeployTroops:
                Deploy();
                break;

            case SpecialAction.Knockback:
                Shove();
                break;
        }
    }

    /// <summary>
    /// The answer to being kited. Everything else in the roster can be outrun, which at
    /// size 5 makes a heavy a stationary target you circle; this takes the spacing away
    /// and points you at whatever is behind you.
    ///
    /// One action, two feels, both from the type's own numbers: the Abomination's roar
    /// is a wide ring that deals nothing and throws you a long way, a mech's punch is a
    /// short reach that hurts and shoves you a shorter distance.
    ///
    /// The ring is drawn whether or not it connects, so the radius is something the
    /// player can learn rather than guess at.
    /// </summary>
    void Shove()
    {
        AudioEvents.Play(type.specialSound, transform.position, owner: gameObject);
        HitFx.Burst(transform.position, new Color(1f, 0.55f, 0.25f),
                    type.specialRange, tuning.pixelsPerUnit, 0.5f);

        var progress = PlayerProgress.Instance;
        if (progress == null || progress.RunOver) return;

        progress.ShakeExternal(tuning.tierUpShake * 1.2f);

        Vector2 d = (Vector2)progress.transform.position - (Vector2)transform.position;
        if (new Vector2(d.x, d.y / tuning.isoSquash).magnitude > type.specialRange) return;

        if (type.specialDamage > 0f) progress.TakeDamage(type.specialDamage);

        var controller = progress.GetComponent<PlayerController>();
        if (controller == null) return;

        float distance = type.specialKnockback > 0f ? type.specialKnockback : tuning.knockbackDistance;
        controller.Knockback(transform.position, distance, tuning.knockbackSeconds);
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

    public override void TakeDamage(float amount, Vector2 from) => TakeDamage(amount, from, false);

    /// <summary>
    /// <paramref name="ignoreArmour"/> is for damage that is not a strike. Poison is
    /// the case that needs it: at 1.6 a tick it is under every heavy's armour value,
    /// so a flat subtraction deletes it entirely and the upgrade quietly stops
    /// existing against exactly the enemies it was bought to handle.
    /// </summary>
    public void TakeDamage(float amount, Vector2 from, bool ignoreArmour)
    {
        if (!IsAlive) return;

        // Armour is flat subtraction, so chip damage genuinely bounces off heavies.
        float dealt = ignoreArmour ? amount : Mathf.Max(0f, amount - type.armour);

        if (dealt <= 0f)
        {
            // Still flashes and still makes its sound — the hit reads as landing and
            // doing nothing. The word was doing the same job as the flash and the
            // sound, three times over, on every chip against every heavy on screen.
            flashUntil = Time.time + 0.06f;
            AudioEvents.Play(Sfx.EnemyBlocked, transform.position, owner: gameObject);
            return;
        }

        hp -= dealt;
        if (art != null && hp > 0f) art.Play(EnemyArt.Hit, true);
        if (hp > 0f) ShowBar();
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
        if (bar != null) { Destroy(bar.gameObject); bar = null; }
        if (sound == Sfx.EnemyDeath) AudioEvents.Play(Sfx.EnemyDeath, transform.position, 0.5f, owner: gameObject);
        Food.Scatter(tuning, transform.position, type.foodDrops, type.foodScatter, tuning.pixelsPerUnit);

        if (type.dropsUpgrade && PlayerUpgrades.Instance != null)
            UpgradePickup.Spawn(tuning, PlayerUpgrades.Instance.RollDrop(),
                                transform.position, tuning.pixelsPerUnit);

        // The boss is the run's end condition. Announced here rather than from the
        // director so it holds for any enemy flagged isBoss, however it got spawned —
        // including the F-key cheat, which is the only way anyone tests this.
        if (IsBoss && PlayerProgress.Instance != null) PlayerProgress.Instance.Win();

        // With a destruction clip, the corpse lingers just long enough to play it.
        // Collision goes immediately so a dying enemy never blocks or shoves.
        if (art != null && sound != Sfx.Squish && type.deathFrames > 0)
        {
            dying = true;
            destroyAt = Time.time + type.deathFrames / DirectionalArt.Fps + 0.15f;
            art.Play(EnemyArt.Death, true);

            foreach (var c in GetComponents<Collider2D>()) c.enabled = false;
            body.linearVelocity = Vector2.zero;
            return;
        }

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
