using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swipe — the auto attack. Fires on a cooldown with no input, Vampire Survivors
/// style, hitting everything in an arc in front of the kaiju.
/// </summary>
[SoundActions(Sfx.Swipe, Sfx.SwipeHitEnemy, Sfx.SwipeHitBuilding)]
public class PlayerAttack : MonoBehaviour
{
    public Tuning tuning;

    public float CooldownRemaining { get; private set; }
    public float LastSwipeAt { get; private set; } = -99f;

    PlayerController player;
    readonly List<Collider2D> hits = new();
    ContactFilter2D filter;
    int pendingHits;
    float nextHitAt;

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

        // Land any remaining hits of the current burst. Claws turns one activation
        // into several in quick succession, so the rhythm changes rather than the
        // numbers: X...X...X becomes XX...XX...XX.
        if (pendingHits > 0 && Time.time >= nextHitAt)
        {
            pendingHits--;
            nextHitAt = Time.time + tuning.swipeBurstInterval;
            // Later hits of a burst deal damage but do not restart the animation:
            // at 0.13s apart they retriggered it faster than it could play, so the
            // swing never got past its opening frames.
            Swipe(playAnimation: false);
        }

        CooldownRemaining -= Time.deltaTime;
        if (CooldownRemaining > 0f || pendingHits > 0) return;

        var up = PlayerUpgrades.Instance;
        CooldownRemaining = tuning.swipeCooldown * (up != null ? up.SwipeCooldownMul : 1f);

        Swipe(playAnimation: true);
        pendingHits = up != null ? up.SwipeExtraHits : 0;
        nextHitAt = Time.time + tuning.swipeBurstInterval;
    }

    void Swipe(bool playAnimation)
    {
        LastSwipeAt = Time.time;
        AudioEvents.Play(Sfx.Swipe, transform.position, 0.4f, owner: gameObject);

        var upgrades = PlayerUpgrades.Instance;
        float range = tuning.swipeRange * player.Scale *
                      (upgrades != null ? upgrades.SwipeRangeMul : 1f);
        Vector2 origin = transform.position;
        Vector2 aim = player.AimDir;

        // Claws widens the cone as well as lengthening it, up to the cap.
        float arc = Mathf.Min(tuning.swipeArcMax,
                              tuning.swipeArc + (upgrades != null ? upgrades.SwipeArcBonus : 0f));

        SwipeFx.Show(tuning, transform.position, aim, range, arc, tuning.pixelsPerUnit);
        float cosHalfArc = Mathf.Cos(arc * 0.5f * Mathf.Deg2Rad);

        int count = Physics2D.OverlapCircle(origin, range, filter, hits);
        bool hitEnemy = false;
        bool hitBuilding = false;
        bool hitOther = false;

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
            if (target is Enemy) hitEnemy = true;
            else if (target is Building) hitBuilding = true;
            else hitOther = true;
        }

        // The swing animation only plays when the swipe actually connects. The attack
        // is automatic and fires on a cooldown forever, so animating every one meant a
        // kaiju standing alone in an empty street swiped at nothing every two seconds,
        // interrupting idle over and over.
        if (playAnimation && (hitEnemy || hitBuilding || hitOther) && UriesArt.Instance != null)
            UriesArt.Instance.PlayOnce(UriesArt.Clip.Swipe);

        // Once per target kind per swipe, even when several targets are hit.
        // A mixed swipe plays both impact sounds, using the player's size settings.
        if (hitEnemy) AudioEvents.Play(Sfx.SwipeHitEnemy, transform.position, 0.6f, owner: gameObject);
        if (hitBuilding) AudioEvents.Play(Sfx.SwipeHitBuilding, transform.position, 0.6f, owner: gameObject);
        if (hitOther) AudioEvents.Play(Sfx.SwipeHit, transform.position, 0.6f, owner: gameObject);
    }
}
