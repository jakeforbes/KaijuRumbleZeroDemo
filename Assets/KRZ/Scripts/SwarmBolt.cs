using UnityEngine;

/// <summary>
/// One homing energy particle from the Swarm upgrade. The player's mirror of the
/// Mech's missile, and deliberately a separate class rather than a flag on Missile:
/// this one picks a target and re-picks when that target dies, which the enemy
/// version — always aimed at the one player — never has to do.
///
/// Like Missile, it carries no Collider2D. It steers by position and checks its own
/// distance, so a discharge of eight never shoves itself apart or wakes the solver.
///
/// Two phases. It gathers around the kaiju first, then discharges. The gather is
/// what makes the ability readable: on a five second timer, something has to say
/// "now" before the damage happens, or particles just appear.
/// </summary>
public class SwarmBolt : MonoBehaviour
{
    static Sprite sprite;
    static Transform root;

    Tuning tuning;
    Transform origin;
    Enemy target;

    float damage;
    float launchAt;
    float diesAt;
    float orbitAngle;
    float orbitRadius;
    Vector2 velocity;
    SpriteRenderer sr;

    public static void Reset() => root = null;

    /// <summary>
    /// Spawns one discharge. Targets are handed out round robin, so with more
    /// particles than enemies the extras double up on what is already there rather
    /// than being wasted.
    /// </summary>
    public static void Discharge(Tuning tuning, Transform from, int count, float damage, float ppu)
    {
        if (count <= 0) return;
        if (root == null) root = new GameObject("Swarm").transform;
        if (sprite == null) sprite = GreyboxArt.Pickup(14, Color.white, ppu);

        var targets = FindTargets(tuning, from.position);
        if (targets.Count == 0) return;

        float bodyHeight = PlayerProgress.Instance != null ? PlayerProgress.Instance.Scale : 1f;

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("bolt");
            go.transform.SetParent(root, false);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(0.65f, 1f, 0.85f);
            sr.sortingOrder = 3;

            var b = go.AddComponent<SwarmBolt>();
            b.tuning = tuning;
            b.sr = sr;
            b.origin = from;
            b.damage = damage;
            b.target = targets[i % targets.Count];
            b.orbitAngle = i / (float)count * Mathf.PI * 2f;
            b.orbitRadius = bodyHeight * 0.55f;
            b.launchAt = Time.time + tuning.swarmFormSeconds;
            b.diesAt = b.launchAt + tuning.swarmLife;

            go.transform.position = b.OrbitPoint(bodyHeight);
        }

        AudioEvents.Play(Sfx.Swarm, from.position, owner: from.gameObject);
    }

    static System.Collections.Generic.List<Enemy> FindTargets(Tuning tuning, Vector3 from)
    {
        var found = new System.Collections.Generic.List<Enemy>();
        foreach (var e in Enemy.All)
        {
            if (e == null || !e.IsAlive) continue;
            Vector2 d = e.transform.position - from;
            if (new Vector2(d.x, d.y / tuning.isoSquash).magnitude <= tuning.swarmRange) found.Add(e);
        }
        return found;
    }

    Vector3 OrbitPoint(float bodyHeight)
    {
        if (origin == null) return transform.position;

        return origin.position
             + new Vector3(Mathf.Cos(orbitAngle) * orbitRadius,
                           Mathf.Sin(orbitAngle) * orbitRadius * tuning.isoSquash
                           + bodyHeight * 0.5f, 0f);
    }

    void Update()
    {
        if (Time.time >= diesAt) { Destroy(gameObject); return; }

        if (Time.time < launchAt)
        {
            Gather();
            return;
        }

        // The one it was assigned may have died while the swarm was still forming.
        if (target == null || !target.IsAlive)
        {
            var replacements = FindTargets(tuning, transform.position);
            if (replacements.Count == 0) { Fizzle(); return; }
            target = replacements[Random.Range(0, replacements.Count)];
        }

        Vector2 toTarget = target.transform.position - transform.position;
        var flat = new Vector2(toTarget.x, toTarget.y / tuning.isoSquash);

        // Steer on the unsquashed plane so the arc is a real curve on the ground,
        // then squash the result back for movement.
        var wanted = flat.normalized * tuning.swarmSpeed;
        wanted = new Vector2(wanted.x, wanted.y * tuning.isoSquash);

        velocity = Vector2.MoveTowards(velocity, wanted, tuning.swarmTurn * Time.deltaTime);
        transform.position += (Vector3)velocity * Time.deltaTime;

        if (flat.magnitude > tuning.swarmHitRadius) return;

        HitFx.Burst(transform.position, new Color(0.7f, 1f, 0.9f), 0.6f, tuning.pixelsPerUnit, 0.15f);
        target.TakeDamage(damage, transform.position);
        Destroy(gameObject);
    }

    /// <summary>Circling the kaiju and tightening in, so the discharge reads as a release.</summary>
    void Gather()
    {
        if (origin == null) { Destroy(gameObject); return; }

        float bodyHeight = PlayerProgress.Instance != null ? PlayerProgress.Instance.Scale : 1f;
        orbitAngle += 7f * Time.deltaTime;

        float remaining = Mathf.InverseLerp(launchAt, launchAt - tuning.swarmFormSeconds, Time.time);
        orbitRadius = bodyHeight * Mathf.Lerp(0.25f, 0.7f, remaining);

        transform.position = OrbitPoint(bodyHeight);

        // Brightening as it tightens, so the moment of release is telegraphed.
        if (sr != null) sr.color = new Color(0.65f, 1f, 0.85f, Mathf.Lerp(1f, 0.45f, remaining));
    }

    /// <summary>Nothing left to hit. Fades rather than vanishing on a frame boundary.</summary>
    void Fizzle()
    {
        HitFx.Burst(transform.position, new Color(0.65f, 1f, 0.85f), 0.35f, tuning.pixelsPerUnit, 0.2f);
        Destroy(gameObject);
    }
}
