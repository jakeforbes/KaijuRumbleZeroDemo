using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Blast — the manually triggered special — and Stomp, which the upgrade adds.
///
/// Blast damages everything in a line, which is why it answers armour: the swipe
/// chips, the Blast lands one number big enough to matter.
/// </summary>
public class PlayerSpecial : MonoBehaviour
{
    public Tuning tuning;

    public float BlastCooldownRemaining { get; private set; }
    public float BlastCooldownTotal { get; private set; } = 1f;

    PlayerController player;
    PlayerUpgrades upgrades;
    readonly List<Collider2D> hits = new();
    ContactFilter2D filter;
    float nextStompAt;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        upgrades = GetComponent<PlayerUpgrades>();

        filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null || progress.IsDead) return;

        BlastCooldownRemaining -= Time.deltaTime;
        if (Pressed() && BlastCooldownRemaining <= 0f) Blast(progress);

        if (upgrades.HasStomp && Time.time >= nextStompAt)
        {
            nextStompAt = Time.time + tuning.stompCooldown;
            Stomp(progress);
        }
    }

    static bool Pressed()
    {
        var kb = Keyboard.current;
        if (kb != null && (kb.spaceKey.wasPressedThisFrame || kb.eKey.wasPressedThisFrame)) return true;

        var pad = Gamepad.current;
        return pad != null && (pad.buttonSouth.wasPressedThisFrame ||
                               pad.rightTrigger.wasPressedThisFrame);
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

        AudioEvents.Play(Sfx.Blast, origin);
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

            HitFx.Line(muzzle, muzzle + (Vector3)(screenDir * range), new Color(0.55f, 0.9f, 1f),
                       tuning.pixelsPerUnit, 0.22f, width);

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
            HitFx.Burst(hits[i].ClosestPoint(origin), new Color(0.6f, 0.95f, 1f), 0.7f, tuning.pixelsPerUnit);
        }
    }

    void Stomp(PlayerProgress progress)
    {
        float radius = tuning.stompRadius * Mathf.Pow(progress.Scale, tuning.stompRadiusExponent);
        float damage = tuning.stompDamage * upgrades.StompPowerMul * progress.DamageMultiplier;

        Vector2 origin = transform.position;
        HitFx.Burst(origin, new Color(1f, 0.85f, 0.4f), radius * 1.6f, tuning.pixelsPerUnit, 0.25f);

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
