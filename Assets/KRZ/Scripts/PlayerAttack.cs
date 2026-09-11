using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Swipe — the auto attack. Fires on a cooldown with no input, Vampire Survivors
/// style, hitting everything in an arc in front of the kaiju.
/// </summary>
[SoundActions(Sfx.Swipe, Sfx.SwipeHitEnemy, Sfx.SwipeHitBuilding, Sfx.SwipeHit)]
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
    bool strikePending;
    float strikeAt;

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
        if (PlayerProgress.Instance != null && PlayerProgress.Instance.RunOver) return;

        var up = PlayerUpgrades.Instance;

        // The swing was started earlier; this is the frame its contact lands on.
        if (strikePending && Time.time >= strikeAt)
        {
            strikePending = false;
            Swipe();
            pendingHits = up != null ? up.SwipeExtraHits : 0;
            nextHitAt = Time.time + tuning.swipeBurstInterval;
        }

        // Remaining hits of a Brawler burst. Multi-hit changes the rhythm
        // rather than the numbers: X...X...X becomes XX...XX...XX. These land damage
        // without restarting the animation, which at 0.13s apart would retrigger it
        // faster than it could play.
        if (pendingHits > 0 && Time.time >= nextHitAt)
        {
            pendingHits--;
            nextHitAt = Time.time + tuning.swipeBurstInterval;
            Swipe();
        }

        CooldownRemaining -= Time.deltaTime;
        if (CooldownRemaining > 0f || strikePending || pendingHits > 0) return;

        // Swings on its rhythm whether or not anything is in reach. This is an
        // auto-attack: the swing is the readout for attack speed, so hiding it when
        // you miss would misreport the one thing Brawler and cooldown upgrades change.
        CooldownRemaining = tuning.swipeCooldown * (up != null ? up.SwipeCooldownMul : 1f);

        // Animation first, damage on the contact frame. Landing damage on frame 0 put
        // the hit before the arm had moved.
        if (UriesArt.Instance != null) UriesArt.Instance.PlayOnce(UriesArt.Clip.Swipe);
        strikePending = true;
        strikeAt = Time.time + tuning.swipeContactDelay;
    }

    void GetArc(out Vector2 origin, out Vector2 aimFlat, out float range, out float cosHalfArc)
    {
        // Reach and cone scale with the kaiju alone now. No upgrade widens them:
        // a bigger cone is nearly impossible to read in play, which is why that
        // upgrade became Prism and moved onto the Blast, where extra beams are
        // unmistakable.
        range = tuning.swipeRange * player.Scale;
        cosHalfArc = Mathf.Cos(tuning.swipeArc * 0.5f * Mathf.Deg2Rad);

        origin = transform.position;
        Vector2 aim = player.AimDir;
        aimFlat = new Vector2(aim.x, aim.y / tuning.isoSquash).normalized;
    }

    /// <summary>
    /// Measured to the nearest point on the collider, not the object's centre. A tower's
    /// centre can be metres from the face you are standing against, so centre-based
    /// angles reject hits that visibly connect.
    /// </summary>
    bool InArc(Collider2D col, Vector2 origin, Vector2 aimFlat, float cosHalfArc)
    {
        Vector2 delta = col.ClosestPoint(origin) - origin;

        // Origin inside the collider: you are standing in it, so it is a hit.
        if (delta.sqrMagnitude <= 0.0001f) return true;

        Vector2 flat = new Vector2(delta.x, delta.y / tuning.isoSquash).normalized;
        return Vector2.Dot(flat, aimFlat) >= cosHalfArc;
    }

    /// <summary>The contact itself: the moment the swing connects and damage lands.</summary>
    void Swipe()
    {
        LastSwipeAt = Time.time;
        AudioEvents.Play(Sfx.Swipe, transform.position, 0.4f, owner: gameObject);

        GetArc(out Vector2 origin, out Vector2 aimFlat, out float range, out float cosHalfArc);

        SwipeFx.Show(tuning, transform.position, player.AimDir, range, tuning.swipeArc,
                     tuning.pixelsPerUnit);

        int count = Physics2D.OverlapCircle(origin, range, filter, hits);
        bool hitEnemy = false;
        bool hitBuilding = false;
        bool hitOther = false;

        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;
            if (!InArc(hits[i], origin, aimFlat, cosHalfArc)) continue;

            float mul = PlayerProgress.Instance != null ? PlayerProgress.Instance.DamageMultiplier : 1f;
            target.TakeDamage(tuning.swipeDamage * mul, origin);
            if (target is Enemy) hitEnemy = true;
            else if (target is Building) hitBuilding = true;
            else hitOther = true;
        }

        // Once per target kind per swipe, even when several targets are hit.
        // A mixed swipe plays both impact sounds, using the player's size settings.
        if (hitEnemy) AudioEvents.Play(Sfx.SwipeHitEnemy, transform.position, 0.6f, owner: gameObject);
        if (hitBuilding) AudioEvents.Play(Sfx.SwipeHitBuilding, transform.position, owner: gameObject);
        if (hitOther) AudioEvents.Play(Sfx.SwipeHit, transform.position, 0.6f, owner: gameObject);
    }
}
