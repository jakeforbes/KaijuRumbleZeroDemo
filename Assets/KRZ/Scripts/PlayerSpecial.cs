using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Blast — the manually triggered special — and Stomp, which the upgrade adds.
///
/// Blast damages everything in a line, which is why it answers armour: the swipe
/// chips, the Blast lands one number big enough to matter.
/// </summary>
[SoundActions(Sfx.Blast, Sfx.Swarm, Sfx.Dash)]
public class PlayerSpecial : MonoBehaviour
{
    public Tuning tuning;

    public float BlastCooldownRemaining { get; private set; }
    public float BlastCooldownTotal { get; private set; } = 1f;

    public float DashCooldownRemaining { get; private set; }
    public float DashCooldownTotal { get; private set; } = 1f;

    PlayerController player;
    PlayerUpgrades upgrades;
    PlayerAttack attack;
    readonly List<Collider2D> hits = new();
    ContactFilter2D filter;
    float nextStompAt;
    float nextSwarmAt;

    // Everything the dash in progress has already hit, so one enemy takes one hit per
    // dash no matter how long it spends inside the band.
    readonly HashSet<Damageable> trampled = new();
    float nextTrailAt;
    bool wasDashing;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        upgrades = GetComponent<PlayerUpgrades>();
        attack = GetComponent<PlayerAttack>();

        filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null || progress.RunOver) return;

        BlastCooldownRemaining -= Time.deltaTime;
        if (Pressed() && BlastCooldownRemaining <= 0f) Blast(progress);

        DashCooldownRemaining -= Time.deltaTime;
        if (DashPressed() && DashCooldownRemaining <= 0f && !player.Dashing
            && !player.BeingKnockedBack) StartDash(progress);

        // Runs every frame of the dash rather than once at launch, so what gets hit is
        // what the kaiju actually passed through — a dash cut short against a building
        // damages only what it reached.
        if (player.Dashing) TrampleTick(progress);

        // Slam lands on the frame the dash stops, wherever that turned out to be —
        // including a dash cut short against a building, which is the read the player
        // wants: you end the charge by hitting something.
        if (wasDashing && !player.Dashing) Slam(progress);
        wasDashing = player.Dashing;

        if (upgrades.HasStomp && Time.time >= nextStompAt)
        {
            nextStompAt = Time.time + tuning.stompCooldown;
            Stomp(progress);
        }

        if (upgrades.HasSwarm && Time.time >= nextSwarmAt)
        {
            nextSwarmAt = Time.time + tuning.swarmInterval;
            Swarm(progress);
        }
    }

    /// <summary>
    /// Light homing damage on a long timer, so it reads as something the kaiju does
    /// rather than something the player aims. Deliberately about a size-1 swipe per
    /// particle: the upgrade's worth is that it fires while you are busy elsewhere,
    /// not that any one hit is big.
    /// </summary>
    void Swarm(PlayerProgress progress)
    {
        float damage = tuning.swarmDamage * upgrades.SwarmPowerMul * progress.DamageMultiplier;
        SwarmBolt.Discharge(tuning, transform, upgrades.SwarmBolts, damage, tuning.pixelsPerUnit);
    }

    static bool Pressed()
    {
        // Space and pad A confirm the pause menu as well as firing a Blast.
        if (PauseMenu.BlockingInput) return false;

        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return true;

        var pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame ||
                               pad.rightTrigger.wasPressedThisFrame);
    }

    static bool DashPressed()
    {
        if (PauseMenu.BlockingInput) return false;

        var kb = Keyboard.current;
        if (kb != null && (kb.leftShiftKey.wasPressedThisFrame ||
                           kb.rightShiftKey.wasPressedThisFrame)) return true;

        var pad = Gamepad.current;
        return pad != null && pad.leftTrigger.wasPressedThisFrame;
    }

    /// <summary>
    /// Launches the dash along wherever the kaiju is already headed, falling back to
    /// its facing when standing still — so a dash from a standstill goes somewhere
    /// rather than nowhere, and a dash while running commits to the run.
    ///
    /// Deliberately not the aim direction. On a pad the right stick points the guns
    /// while the left stick drives, and dashing where you are shooting rather than
    /// where you are steering would take the one escape tool and aim it at the danger.
    /// </summary>
    void StartDash(PlayerProgress progress)
    {
        Vector2 flat = player.Velocity;
        flat = new Vector2(flat.x, flat.y / tuning.isoSquash);

        if (flat.sqrMagnitude < 0.04f)
        {
            // Facing is 0..7 from south, clockwise, matching FacingFromInput's angles.
            float deg = player.Facing * 45f;
            flat = new Vector2(Mathf.Sin(deg * Mathf.Deg2Rad), -Mathf.Cos(deg * Mathf.Deg2Rad));
        }

        float distance = tuning.dashDistance
                       * Mathf.Pow(progress.Scale, tuning.dashDistanceExponent);

        DashCooldownTotal = tuning.dashCooldown;
        DashCooldownRemaining = DashCooldownTotal;

        trampled.Clear();
        nextTrailAt = 0f;

        player.Dash(flat, distance, tuning.dashSeconds);
        AudioEvents.Play(Sfx.Dash, transform.position, owner: gameObject);
    }

    /// <summary>
    /// The blow at the end of the dash. Three times a swipe at the first stack, in the
    /// swipe's own cone, growing in both reach and damage from there.
    ///
    /// Skipped when the dash was ended by being hit: the roar cancels a dash outright,
    /// and landing a heavy blow while being thrown across the street would read as the
    /// game rewarding you for being punished.
    /// </summary>
    void Slam(PlayerProgress progress)
    {
        if (!upgrades.HasSlam || player.BeingKnockedBack || attack == null) return;

        float damage = tuning.swipeDamage * tuning.slamSwipeMultiplier
                     * upgrades.SlamPowerMul * progress.DamageMultiplier;

        attack.Slam(upgrades.SlamRangeMul, damage);
    }

    /// <summary>
    /// One frame of the dash's damage and trail. Both are Trample's — an unupgraded
    /// dash is pure movement, so the particles are the tell that it now hurts.
    ///
    /// The band test matches the Blast's exactly, measured on the flat plane against
    /// the dash heading, so "in its path" means the same thing for both abilities.
    /// </summary>
    void TrampleTick(PlayerProgress progress)
    {
        if (!upgrades.HasTrample) return;

        Vector2 origin = transform.position;
        float bodyHeight = progress.Scale;
        float halfWidth = tuning.trampleWidthFraction * bodyHeight * 0.5f;

        if (Time.time >= nextTrailAt)
        {
            nextTrailAt = Time.time + Mathf.Max(0.01f, tuning.trampleTrailInterval);
            ShockwaveFx.Show(origin, tuning.trampleColour, bodyHeight * 0.42f, .5f, .3f);
        }

        float damage = tuning.trampleDamage * upgrades.TramplePowerMul * progress.DamageMultiplier;
        if (damage <= 0f) return;

        Vector2 heading = player.DashFlatDir;

        // Reach only as far as the body plus the band, not down the whole dash: the
        // sweep is covered by testing every frame as the kaiju travels.
        float reach = halfWidth + bodyHeight;
        int count = Physics2D.OverlapCircle(origin, reach, filter, hits);
        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;

            // Buildings are left alone. A dash that also demolished what it clipped
            // would flatten a street per press and take the choice of what to smash
            // away from the player, the same reason Stomp only chips them.
            if (target is not Enemy) continue;
            if (!trampled.Add(target)) continue;

            Vector2 delta = hits[i].ClosestPoint(origin) - origin;
            Vector2 flat = new Vector2(delta.x, delta.y / tuning.isoSquash);

            float across = Mathf.Abs(flat.x * heading.y - flat.y * heading.x);
            if (across > halfWidth) { trampled.Remove(target); continue; }

            target.TakeDamage(damage, origin);
            ShockwaveFx.Show(hits[i].ClosestPoint(origin), tuning.trampleColour, .4f, .5f, .24f);
        }
    }

    void Blast(PlayerProgress progress)
    {
        float power = upgrades.BlastPowerMul;
        float range = tuning.blastRange * power;
        float damage = tuning.blastDamage * power * progress.DamageMultiplier;

        // The kaiju's drawn height is exactly Scale in world units: a 512 px sprite at
        // 128 PPU is 4 units, times the 0.25 canvas scale, times Scale. So beam
        // thickness and height are both simple fractions of Scale, and the damage band
        // is the same width as the drawing rather than a fixed number beside it.
        float bodyHeight = progress.Scale;
        float width = tuning.blastWidthFraction * bodyHeight;
        float halfWidth = width * 0.5f;

        BlastCooldownTotal = tuning.blastCooldown * upgrades.BlastCooldownMul;
        BlastCooldownRemaining = BlastCooldownTotal;

        Vector2 origin = transform.position;
        Vector2 aim = player.AimDir.sqrMagnitude > 0.001f ? player.AimDir.normalized : Vector2.down;

        // Measured on the flat ground plane so the beam is straight in world terms
        // rather than bent by the projection.
        Vector2 aimFlat = new Vector2(aim.x, aim.y / tuning.isoSquash).normalized;

        AudioEvents.Play(Sfx.Blast, origin, owner: gameObject);
        if (UriesArt.Instance != null) UriesArt.Instance.PlayOnce(UriesArt.Clip.Blast);

        // Centred on the body rather than fired from the head. Hit detection still runs
        // on the ground plane where every footprint lives, so a beam as thick as most
        // of the kaiju visually covers the strip it damages instead of floating above it.
        Vector3 muzzle = (Vector3)origin + Vector3.up * (tuning.blastOriginFraction * bodyHeight);
        progress.ShakeExternal(tuning.hitShake * 1.4f);

        // Prism spreads the Blast evenly around the kaiju: 1 beam, then 2 opposed,
        // then a cross, then an eight-point star. Each beam is a full-strength copy —
        // the upgrade is about covering angles you would otherwise have to turn to face.
        int beams = upgrades.BlastBeams;
        for (int b = 0; b < beams; b++)
        {
            float turn = b * Mathf.PI * 2f / beams;

            // Rotate on the flat ground plane, then squash back, so the star is even
            // on the ground rather than an oval in screen space.
            var flatDir = new Vector2(
                aimFlat.x * Mathf.Cos(turn) - aimFlat.y * Mathf.Sin(turn),
                aimFlat.x * Mathf.Sin(turn) + aimFlat.y * Mathf.Cos(turn));

            var screenDir = new Vector2(flatDir.x, flatDir.y * tuning.isoSquash).normalized;

            EnergyBeamFx.Show(muzzle, muzzle + (Vector3)(screenDir * range),
                              new Color(0.55f, 0.9f, 1f), width, 0.22f);

            FireBeam(origin, flatDir, range, halfWidth, damage);
        }
    }

    /// <summary>Damages everything in one beam's band. Called once per Prism beam.</summary>
    void FireBeam(Vector2 origin, Vector2 aimFlat, float range, float halfWidth, float damage)
    {
        int count = Physics2D.OverlapCircle(origin, range, filter, hits);
        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;

            Vector2 delta = hits[i].ClosestPoint(origin) - origin;
            Vector2 flat = new Vector2(delta.x, delta.y / tuning.isoSquash);

            float along = Vector2.Dot(flat, aimFlat);
            if (along < 0f || along > range) continue;

            float across = Mathf.Abs(flat.x * aimFlat.y - flat.y * aimFlat.x);
            if (across > halfWidth) continue;

            target.TakeDamage(damage, origin);
            ShockwaveFx.Show(hits[i].ClosestPoint(origin), new Color(0.6f, 0.95f, 1f), .35f, .5f, .24f);
        }
    }

    void Stomp(PlayerProgress progress)
    {
        float radius = tuning.stompRadius * Mathf.Pow(progress.Scale, tuning.stompRadiusExponent);
        float damage = tuning.stompDamage * upgrades.StompPowerMul * progress.DamageMultiplier;

        Vector2 origin = transform.position;
        // The old diamond was 1.6 * radius wide and half as tall.
        ShockwaveFx.Show(origin, new Color(1f, .67f, .28f), radius * .8f, .5f, .45f, displacement: true);

        int count = Physics2D.OverlapCircle(origin, radius, filter, hits);
        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;

            // Buildings take a fraction. Stomp is automatic and unaimed, so at full
            // strength it flattens whatever you stand near and takes the choice of
            // what to smash away from the player. Enemies still take the full hit.
            float dealt = target is Building ? damage * tuning.stompBuildingMultiplier : damage;
            target.TakeDamage(dealt, origin);
        }
    }
}
