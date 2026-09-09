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
    static Transform root;

    SpriteRenderer sr;
    float life;
    float age;

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
        float t = 1f - Mathf.Clamp01(age / life);
        var c = sr.color;
        c.a = t;
        sr.color = c;
        if (age >= life) Destroy(gameObject);
    }
}
