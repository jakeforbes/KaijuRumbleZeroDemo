using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Skeleton's special: a bone thrown out along the aim and caught on the way back.
///
/// Out is a straight line, home is a chase. That asymmetry is the whole ability — you commit
/// to a direction, but you never have to stand still to get it back, so the Skeleton can throw
/// and keep running where the Blast asks you to stop and point. It also means the bone sweeps
/// two different lanes on one press, and the return lane is one the player steers by moving.
///
/// Like Missile and SwarmBolt it carries no Collider2D and tests its own overlap each frame, so
/// a bone in flight never shoves anything or wakes the solver. The object itself stays on the
/// ground plane where every footprint lives and the drawn bone is a child lifted to chest
/// height, which is the same split the kaiju's own art uses: what you see is above what hits.
/// </summary>
public class BoneBoomerang : MonoBehaviour
{
    static Transform root;

    /// <summary>Static parent has to be dropped by hand, or a restart re-parents to a dead object.</summary>
    public static void Reset() => root = null;

    static readonly Color Trail = new(0.95f, 0.93f, 0.86f);

    Tuning tuning;
    Transform owner;
    Transform art;

    Vector2 outbound;        // ground-plane heading, normalised; fixed for the whole throw
    float speed, range, damage, hitRadius, catchRadius, spin;
    float travelled;
    bool returning;
    float diesAt;

    ContactFilter2D filter;
    readonly List<Collider2D> hits = new();

    /// <summary>
    /// Cleared when the bone turns for home, so an enemy standing in the lane is hit once
    /// going out and once coming back rather than once per frame it overlaps.
    /// </summary>
    readonly HashSet<Damageable> struck = new();

    /// <summary>
    /// Throws one bone. Direction is the flat ground-plane heading, already unsquashed by the
    /// caller the same way the Blast's is.
    /// </summary>
    public static void Throw(Tuning tuning, Transform from, Vector2 flatDir,
                             float damage, float range, float speed, float bodyHeight, float ppu)
    {
        if (from == null || flatDir.sqrMagnitude < 0.0001f) return;
        if (root == null) root = new GameObject("Bones").transform;

        var go = new GameObject("bone");
        go.transform.SetParent(root, false);
        go.transform.position = from.position;

        // The drawing is a child so the spin cannot rotate the thing that measures hits, and
        // so the bone can ride at chest height while its overlap test stays on the ground.
        var artGo = new GameObject("art");
        artGo.transform.SetParent(go.transform, false);
        artGo.transform.localPosition = Vector3.up * (tuning.boneOriginFraction * bodyHeight);
        artGo.transform.localScale = Vector3.one * Mathf.Max(0.1f, bodyHeight * tuning.boneArtScale);

        var sr = artGo.AddComponent<SpriteRenderer>();
        sr.sprite = GreyboxArt.Bone(tuning.bonePx, ppu);

        var b = go.AddComponent<BoneBoomerang>();
        b.tuning = tuning;
        b.owner = from;
        b.art = artGo.transform;
        b.outbound = flatDir.normalized;
        b.damage = damage;
        b.range = range;

        // Passed in rather than read from Tuning: it is a multiple of the thrower's own speed,
        // which only PlayerSpecial can work out, and it is fixed at the throw so a Speed pickup
        // collected mid-flight cannot leave a bone permanently trailing its owner.
        b.speed = speed;
        b.spin = tuning.boneSpinDegrees;
        b.hitRadius = tuning.boneHitFraction * bodyHeight;
        b.catchRadius = tuning.boneCatchFraction * bodyHeight;
        b.diesAt = Time.time + tuning.boneLife;

        b.filter = new ContactFilter2D();
        b.filter.NoFilter();
        b.filter.useTriggers = true;
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null || progress.RunOver) { Destroy(gameObject); return; }

        // The lifetime cap is not decoration: a bone thrown at the instant the kaiju dies has
        // nothing to come home to, and without this it would orbit the wreckage forever.
        if (owner == null || Time.time >= diesAt) { Vanish(); return; }

        float step = speed * Time.deltaTime;

        if (!returning)
        {
            Advance(outbound, step);
            travelled += step;

            // Turning at range rather than on a timer, so the throw reaches the same distance
            // whatever the frame rate did on the way.
            if (travelled >= range) { returning = true; struck.Clear(); }
        }
        else
        {
            Vector2 delta = owner.position - transform.position;
            var home = new Vector2(delta.x, delta.y / tuning.isoSquash);

            if (home.magnitude <= catchRadius) { Catch(); return; }

            // Re-aimed every frame at where the kaiju is now, not where it was thrown from.
            // That is what lets you throw and keep running.
            Advance(home.normalized, step);
        }

        if (art != null) art.Rotate(0f, 0f, spin * Time.deltaTime);
        Strike();
    }

    /// <summary>
    /// Moves a flat heading's worth of distance. Speed is measured on the ground plane and the
    /// step is squashed on the way out, so a bone thrown north covers as much ground as one
    /// thrown east instead of merely looking like it does.
    /// </summary>
    void Advance(Vector2 flat, float step)
    {
        var screen = new Vector2(flat.x, flat.y * tuning.isoSquash);
        transform.position += (Vector3)(screen * step);
    }

    void Strike()
    {
        Vector2 at = transform.position;
        int count = Physics2D.OverlapCircle(at, hitRadius, filter, hits);

        for (int i = 0; i < count; i++)
        {
            var target = hits[i].GetComponentInParent<Damageable>();
            if (target == null || !target.IsAlive) continue;

            // One hit per target per leg. Without this a slow bone passing through a Goliath
            // would bill it every frame it overlapped and delete it on the spot.
            if (!struck.Add(target)) continue;

            // Buildings take it in full, like the Blast this replaces. The Skeleton still has
            // a city to knock down and this is its only way to reach past a swipe.
            target.TakeDamage(damage, at);
            ShockwaveFx.Show(hits[i].ClosestPoint(at), Trail, 0.32f, 0.5f, 0.22f);
        }
    }

    /// <summary>Back in hand. Reads as a catch rather than as the bone blinking out.</summary>
    void Catch()
    {
        ShockwaveFx.Show(transform.position, Trail, 0.26f, 0.5f, 0.18f);
        Destroy(gameObject);
    }

    void Vanish()
    {
        ShockwaveFx.Show(transform.position, Trail, 0.2f, 0.5f, 0.22f);
        Destroy(gameObject);
    }
}
