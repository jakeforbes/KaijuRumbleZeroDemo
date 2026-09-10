using UnityEngine;

/// <summary>
/// Short-lived marks that show an attack actually happening: a tracer for ranged
/// hits, a burst at the point of impact. Tuning aids and stand-ins, replaced by
/// real VFX in Stage 9.
/// </summary>
public class HitFx : MonoBehaviour
{
    static Sprite barSprite;
    static Sprite burstSprite;
    static Sprite debrisSprite;
    static Transform root;

    SpriteRenderer sr;
    float life;
    float age;
    bool debris;
    bool landed;
    Vector3 ground;
    Vector2 drift;
    float height, lift, spin;
    float gravity;
    float flightTime;

    /// <summary>Chips fall from the building's visible centre along the strike.</summary>
    public static void BuildingDebris(Vector3 at, Vector2 direction, Color colour, float ppu, float groundY, BuildingType settings)
    {
        if (!settings.debrisEnabled) return;
        if (debrisSprite == null) debrisSprite = GreyboxArt.Solid(8, 5, Color.white, ppu);
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.up;
        direction.Normalize();
        for (int i = 0; i < Mathf.Clamp(settings.debrisCount, 0, 100); i++)
        {
            var go = New("building chip", at, colour, 2);
            var renderer = go.GetComponent<SpriteRenderer>();
            renderer.sprite = debrisSprite;
            renderer.color = Color.Lerp(colour, new Color(0.55f, 0.57f, 0.63f), Random.Range(0.2f, 0.65f));
            go.transform.localScale = Vector3.one * Range(settings.debrisSize, 0.01f);
            var fx = go.AddComponent<HitFx>();
            fx.sr = renderer;
            fx.debris = true;
            fx.ground = new Vector3(at.x, Mathf.Min(groundY, at.y), at.z);
            float spread = Mathf.Clamp(settings.debrisSpread, 0, 180);
            fx.drift = (Vector2)(Quaternion.Euler(0, 0, Random.Range(-spread, spread)) * direction)
                * Range(settings.debrisForce, 0);
            fx.gravity = Mathf.Max(0.1f, settings.debrisGravity);
            fx.height = Mathf.Max(0, at.y - fx.ground.y);
            fx.lift = 0;
            fx.spin = Random.Range(-360f, 360f);
            fx.flightTime = Mathf.Sqrt(2f * fx.height / fx.gravity);
            fx.life = fx.flightTime + 0.2f;
        }
    }

    static float Range(Vector2 range, float minimum) => Random.Range(
        Mathf.Max(minimum, Mathf.Min(range.x, range.y)),
        Mathf.Max(minimum, Mathf.Max(range.x, range.y)));

    public static void Reset() => root = null;

    /// <summary>
    /// A tracer between two points. Thickness is in world units, so a beam can be
    /// drawn at the width it actually hits rather than as a hairline.
    /// </summary>
    public static void Line(Vector3 from, Vector3 to, Color colour, float ppu,
                            float life = 0.14f, float thickness = 0.1f)
    {
        EnsureSprites(ppu);
        var go = New("tracer", from, colour, 3);

        Vector2 delta = to - from;
        float len = delta.magnitude;
        if (len < 0.01f) return;

        go.transform.right = delta.normalized;
        // The bar sprite is 64 x 8 px with a left-edge pivot, so both axes convert
        // from world units through the sprite's own size.
        go.transform.localScale = new Vector3(len / (64f / ppu), thickness / (8f / ppu), 1f);

        Finish(go, life);
    }

    /// <summary>A quick burst where something was hit.</summary>
    public static void Burst(Vector3 at, Color colour, float size, float ppu, float life = 0.2f)
    {
        EnsureSprites(ppu);
        var go = New("burst", at, colour, 3);
        go.transform.localScale = Vector3.one * (size / (48f / ppu));
        Finish(go, life);
    }

    static GameObject New(string name, Vector3 at, Color colour, int order)
    {
        if (root == null) root = new GameObject("HitFx").transform;

        var go = new GameObject(name);
        go.transform.SetParent(root, false);
        go.transform.position = at;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = name == "tracer" ? barSprite : burstSprite;
        sr.color = colour;
        sr.sortingOrder = order;
        return go;
    }

    static void Finish(GameObject go, float life)
    {
        var fx = go.AddComponent<HitFx>();
        fx.sr = go.GetComponent<SpriteRenderer>();
        fx.life = life;
    }

    static void EnsureSprites(float ppu)
    {
        if (barSprite != null) return;
        barSprite = GreyboxArt.Solid(64, 8, Color.white, ppu, 0f);
        burstSprite = GreyboxArt.Pickup(48, Color.white, ppu);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (debris && !landed)
        {
            ground += (Vector3)drift * Time.deltaTime;
            lift -= gravity * Time.deltaTime;
            height = Mathf.Max(0, height + lift * Time.deltaTime);
            transform.position = ground + Vector3.up * height;
            transform.Rotate(0, 0, spin * Time.deltaTime);
            if (height <= 0f) landed = true;
        }
        // Debris fades out across the second half of its flight, so it is gone by
        // the time it would otherwise sit motionless on the ground. Everything else
        // (tracers, bursts) keeps its original full-lifetime fade.
        float t;
        if (debris)
        {
            float fadeStart = flightTime * 0.5f;
            t = age <= fadeStart ? 1f : 1f - Mathf.Clamp01((age - fadeStart) / Mathf.Max(0.0001f, flightTime - fadeStart));
        }
        else
        {
            t = 1f - Mathf.Clamp01(age / life);
        }
        var c = sr.color;
        c.a = t;
        sr.color = c;
        if (age >= life) Destroy(gameObject);
    }
}
