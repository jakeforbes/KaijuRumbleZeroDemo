using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swipe — the auto attack. Fires on a cooldown with no input, Vampire Survivors
/// style, hitting everything in an arc in front of the kaiju.
/// </summary>
public class PlayerAttack : MonoBehaviour
{
    public Tuning tuning;

    public float CooldownRemaining { get; private set; }
    public float LastSwipeAt { get; private set; } = -99f;

    PlayerController player;
    readonly List<Collider2D> hits = new();
    ContactFilter2D filter;

    void Awake()
    {
        player = GetComponent<PlayerController>();
        // NoFilter() resets every field, so it has to come before useTriggers is set.
        filter = new ContactFilter2D();
        filter.NoFilter();
        filter.useTriggers = true;
    }

    void Update()
    {
        if (PlayerProgress.Instance != null && PlayerProgress.Instance.IsDead) return;

        CooldownRemaining -= Time.deltaTime;
        if (CooldownRemaining > 0f) return;

        var up = PlayerUpgrades.Instance;
        CooldownRemaining = tuning.swipeCooldown * (up != null ? up.SwipeCooldownMul : 1f);
        Swipe();
    }

    void Swipe()
    {
        LastSwipeAt = Time.time;
        AudioEvents.Play(Sfx.Swipe, transform.position, 0.4f);

        var upgrades = PlayerUpgrades.Instance;
        float range = tuning.swipeRange * player.Scale *
                      (upgrades != null ? upgrades.SwipeRangeMul : 1f);
        Vector2 origin = transform.position;
        Vector2 aim = player.AimDir;

        SwipeFx.Show(tuning, transform.position, aim, range, tuning.pixelsPerUnit);
        float cosHalfArc = Mathf.Cos(tuning.swipeArc * 0.5f * Mathf.Deg2Rad);

        int count = Physics2D.OverlapCircle(origin, range, filter, hits);
        bool connected = false;

        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;

            // Aim at the nearest point on the collider, not the object's centre. A
            // tower's centre can be metres from the face you are standing against, so
            // centre-based angles reject hits that visibly connect — which only shows
            // up once the range is short.
            Vector2 delta = hits[i].ClosestPoint(origin) - origin;

            // Origin inside the collider: you are standing in it, so it is a hit.
            if (delta.sqrMagnitude > 0.0001f)
            {
                // Unsquash before measuring the angle, so the arc is the shape it looks
                // like on the ground rather than a squashed version of itself.
                Vector2 flat = new Vector2(delta.x, delta.y / tuning.isoSquash).normalized;
                Vector2 aimFlat = new Vector2(aim.x, aim.y / tuning.isoSquash).normalized;
                if (Vector2.Dot(flat, aimFlat) < cosHalfArc) continue;
            }

            float mul = PlayerProgress.Instance != null ? PlayerProgress.Instance.DamageMultiplier : 1f;
            target.TakeDamage(tuning.swipeDamage * mul, origin);
            connected = true;
        }

        if (connected) AudioEvents.Play(Sfx.SwipeHit, transform.position, 0.6f);
    }
}
