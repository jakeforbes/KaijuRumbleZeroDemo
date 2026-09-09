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
        CooldownRemaining -= Time.deltaTime;
        if (CooldownRemaining > 0f) return;

        CooldownRemaining = tuning.swipeCooldown;
        Swipe();
    }

    void Swipe()
    {
        LastSwipeAt = Time.time;
        AudioEvents.Play(Sfx.Swipe, transform.position, 0.4f);

        float range = tuning.swipeRange * player.Scale;
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

            Vector2 delta = (Vector2)target.transform.position - origin;
            if (delta.sqrMagnitude < 0.0001f) continue;

            // Unsquash before measuring the angle, so the arc is the shape it looks
            // like on the ground rather than a squashed version of itself.
            Vector2 flat = new Vector2(delta.x, delta.y / tuning.isoSquash).normalized;
            Vector2 aimFlat = new Vector2(aim.x, aim.y / tuning.isoSquash).normalized;
            if (Vector2.Dot(flat, aimFlat) < cosHalfArc) continue;

            target.TakeDamage(tuning.swipeDamage * player.Scale, origin);
            connected = true;
        }

        if (connected) AudioEvents.Play(Sfx.SwipeHit, transform.position, 0.6f);
    }
}
