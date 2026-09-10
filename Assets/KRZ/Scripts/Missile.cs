using UnityEngine;

/// <summary>
/// A homing missile from a Mech volley.
///
/// Deliberately has no Collider2D at all: it steers by position and checks its own
/// distance to the player. That is what keeps a cluster of them from shoving each
/// other, colliding with the swarm, or waking the physics solver — and it is far
/// cheaper than eight rigid bodies per volley.
/// </summary>
public class Missile : MonoBehaviour
{
    static Sprite sprite;
    static Transform root;

    Tuning tuning;
    float damage;
    float speed;
    float turn;
    float diesAt;
    Vector2 velocity;

    public static void Reset() => root = null;

    public static void Volley(Tuning tuning, EnemyType type, Vector3 from, float ppu)
    {
        if (root == null) root = new GameObject("Missiles").transform;
        if (sprite == null) sprite = GreyboxArt.Pickup(18, Color.white, ppu);

        for (int i = 0; i < type.volleyCount; i++)
        {
            var go = new GameObject("missile");
            go.transform.SetParent(root, false);
            go.transform.position = from + Vector3.up * (type.bodyPx * 0.5f / ppu);

            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.color = new Color(1f, 0.62f, 0.30f);
            sr.sortingOrder = 3;

            var m = go.AddComponent<Missile>();
            m.tuning = tuning;
            m.damage = type.missileDamage;
            m.speed = type.missileSpeed;
            m.turn = type.missileTurn;
            m.diesAt = Time.time + type.missileLife;

            // Burst outward first, then curve in. Firing them straight at the player
            // reads as one thick line; a spread reads as a swarm.
            float angle = (i / (float)type.volleyCount) * Mathf.PI * 2f + Random.value;
            var dir = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle) * tuning.isoSquash).normalized;
            m.velocity = dir * type.missileSpeed * Random.Range(0.5f, 0.8f);
        }


    }

    void Update()
    {
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

        float hitRadius = tuning.missileHitRadius * progress.Scale;
        if (flat.magnitude <= hitRadius)
        {
            HitFx.Burst(transform.position, new Color(1f, 0.6f, 0.3f), 0.5f, tuning.pixelsPerUnit, 0.15f);
            progress.TakeDamage(damage);
            Destroy(gameObject);
        }
    }
}
