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
        float halfWidth = tuning.blastWidth * 0.5f;
        float damage = tuning.blastDamage * power * progress.DamageMultiplier;

        BlastCooldownTotal = tuning.blastCooldown * upgrades.BlastCooldownMul;
        BlastCooldownRemaining = BlastCooldownTotal;

        Vector2 origin = transform.position;
        Vector2 aim = player.AimDir.sqrMagnitude > 0.001f ? player.AimDir.normalized : Vector2.down;

        // Measured on the flat ground plane so the beam is straight in world terms
        // rather than bent by the projection.
        Vector2 aimFlat = new Vector2(aim.x, aim.y / tuning.isoSquash).normalized;

        AudioEvents.Play(Sfx.Blast, origin);
        // Drawn at the width it actually hits, so range and width are tunable by eye.
        HitFx.Line(origin, origin + aim * range, new Color(0.55f, 0.9f, 1f),
                   tuning.pixelsPerUnit, 0.22f, tuning.blastWidth);
        progress.ShakeExternal(tuning.hitShake * 1.4f);

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
        float radius = tuning.stompRadius * progress.Scale;
        float damage = tuning.stompDamage * upgrades.StompPowerMul * progress.DamageMultiplier;

        Vector2 origin = transform.position;
        HitFx.Burst(origin, new Color(1f, 0.85f, 0.4f), radius * 1.6f, tuning.pixelsPerUnit, 0.25f);

        int count = Physics2D.OverlapCircle(origin, radius, filter, hits);
        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;
            target.TakeDamage(damage, origin);
        }
    }
}
