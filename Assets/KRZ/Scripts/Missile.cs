using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A homing missile from a Mech volley, and something the player can shoot down.
///
/// Flight is still done entirely by position: it steers itself and checks its own
/// distance to the player, which is what keeps a cluster of them from shoving each
/// other or waking the solver. The collider it now carries is a trigger on a
/// kinematic body and exists only so the player's sweeps can find it — nothing
/// physical ever happens to it, and a volley still costs no solver work.
///
/// A missile is as fragile as a Grunt, so any real hit kills one, but half of every
/// hit is jinked away from. That is the whole design: a well-timed swipe into an
/// incoming volley thins it rather than deleting it, so the answer to a Mech stays
/// "cut the volley down and take the rest" instead of "press attack and ignore it".
/// </summary>
public class Missile : Damageable
{
    static Mesh missileMesh;
    static Material missileMaterial;
    static Transform root;
    static readonly int FlamePhase = Shader.PropertyToID("_FlamePhase");

    Tuning tuning;
    float damage;
    float speed;
    float turn;
    float diesAt;
    Vector2 velocity;
    MeshRenderer visual;
    MaterialPropertyBlock properties;
    float flameOffset;
    float hp;

    public override bool IsAlive => hp > 0f;

    /// <summary>
    /// Shot at by the player. Half of all hits are jinked out of the way of rather
    /// than absorbed, and the jink is shown as an actual change of course instead of
    /// a number that fails to appear — a hit that silently does nothing reads as the
    /// game having dropped the input.
    ///
    /// The sidestep is not only feedback: it costs the missile its heading, so a
    /// dodged hit still buys the player the time it takes to turn back.
    /// </summary>
    public override void TakeDamage(float amount, Vector2 from)
    {
        if (!IsAlive) return;

        if (Random.value < tuning.missileDodgeChance)
        {
            var sideways = new Vector2(-velocity.y, velocity.x).normalized;
            if (Random.value < 0.5f) sideways = -sideways;

            velocity = (velocity + sideways * speed * 0.9f).normalized * speed;
            ShockwaveFx.Show(transform.position, new Color(0.75f, 0.88f, 1f), .3f, .5f, .18f);
            return;
        }

        hp -= amount;
        if (IsAlive) return;

        ShockwaveFx.Show(transform.position, new Color(1f, 0.72f, 0.32f), .7f, .5f, .3f);
        Destroy(gameObject);
    }

    public static void Reset() => root = null;

    /// <summary>Shared artwork only; enemy and Swarm missiles retain separate flight logic.</summary>
    public static MeshRenderer CreateVisual(Transform parent, int sizePx, float ppu)
    {
        if (missileMesh == null)
        {
            missileMesh = new Mesh { name = "Shared missile silhouette" };
            // +X is the nose; reserve space behind the body for exhaust.
            missileMesh.vertices = new[] { new Vector3(-1.3f,-.375f,0), new Vector3(.55f,-.375f,0),
                                          new Vector3(.55f,.375f,0), new Vector3(-1.3f,.375f,0) };
            missileMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            missileMesh.triangles = new[] { 0,1,2,0,2,3 };
            missileMesh.RecalculateBounds();
        }
        if (missileMaterial == null)
            missileMaterial = new Material(Resources.Load<Shader>("HomingMissile")) { name = "Homing missile (shared)" };

        var art = new GameObject("Missile body, fins and exhaust");
        art.transform.SetParent(parent, false);
        art.transform.localScale = Vector3.one * (Mathf.Max(4, sizePx) / Mathf.Max(1f, ppu));
        art.AddComponent<MeshFilter>().sharedMesh = missileMesh;
        var renderer = art.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = missileMaterial;
        renderer.sortingOrder = 3;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        return renderer;
    }

    public static void Volley(Tuning tuning, EnemyType type, Vector3 from, float ppu)
    {
        if (root == null) root = new GameObject("Missiles").transform;

        for (int i = 0; i < type.volleyCount; i++)
        {
            var go = new GameObject("missile");
            go.transform.SetParent(root, false);
            go.transform.position = from + Vector3.up * (type.bodyPx * 0.5f / ppu);

            var renderer = CreateVisual(go.transform, tuning.missilePx, ppu);

            // Kinematic body under a trigger collider: the only reason either exists
            // is so the player's OverlapCircle sweeps can find the thing. Kinematic
            // means nothing pushes it and it pushes nothing, and the trigger means no
            // contact is ever resolved, so the flight code below stays authoritative.
            var rb = go.AddComponent<Rigidbody2D>();
            rb.bodyType = RigidbodyType2D.Kinematic;
            rb.gravityScale = 0f;
            rb.freezeRotation = true;

            var col = go.AddComponent<CircleCollider2D>();
            col.isTrigger = true;
            col.radius = Mathf.Max(0.12f, tuning.missilePx * 0.5f / ppu);

            var m = go.AddComponent<Missile>();
            m.tuning = tuning;
            m.hp = tuning.missileHp;
            m.damage = type.missileDamage;
            m.speed = type.missileSpeed;
            m.turn = type.missileTurn;
            m.diesAt = Time.time + type.missileLife;
            m.visual = renderer;
            m.properties = new MaterialPropertyBlock();
            m.flameOffset = i * 2.71f + from.x * .37f + from.y * .53f;
            m.AnimateExhaust();

            // Burst outward first, then curve in. Firing them straight at the player
            // reads as one thick line; a spread reads as a swarm.
            float angle = (i / (float)type.volleyCount) * Mathf.PI * 2f + Random.value;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * tuning.isoSquash).normalized;
            m.velocity = dir * type.missileSpeed * Random.Range(0.5f, 0.8f);
            if (m.velocity.sqrMagnitude > .01f) go.transform.right = m.velocity.normalized;
        }


    }

    void Update()
    {
        if (PlayerProgress.Instance != null && PlayerProgress.Instance.HasWon) return;
        var progress = PlayerProgress.Instance;
        if (progress == null || Time.time >= diesAt) { Destroy(gameObject); return; }

        Vector2 toPlayer = progress.transform.position - transform.position;

        // Steer on the unsquashed plane so the arc is a real curve on the ground
        // rather than a squashed one, then squash the result back for movement.
        var flat = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash);
        var wanted = flat.normalized * speed;
        wanted = new Vector2(wanted.x, wanted.y * tuning.isoSquash);

        velocity = Vector2.MoveTowards(velocity, wanted, turn * Time.deltaTime);
        transform.position += (Vector3)velocity * Time.deltaTime;

        if (velocity.sqrMagnitude > 0.01f)
            transform.right = velocity.normalized;
        AnimateExhaust();

        float hitRadius = tuning.missileHitRadius * progress.Scale;
        if (flat.magnitude <= hitRadius)
        {
            progress.TakeDamage(damage);
            Destroy(gameObject);
        }
    }

    void AnimateExhaust()
    {
        properties.SetFloat(FlamePhase, Time.time * 35f + flameOffset);
        visual.SetPropertyBlock(properties);
    }
}
