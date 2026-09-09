using UnityEngine;

/// <summary>
/// Draws the swipe arc for a fraction of a second so range and width can be tuned
/// by eye. This is a tuning aid, not the real attack visual — Stage 9 replaces it.
/// </summary>
public class SwipeFx : MonoBehaviour
{
    static Sprite cached;
    static float cachedArc = -1f;

    SpriteRenderer sr;
    float life;
    float age;

    public static void Show(Tuning tuning, Vector3 at, Vector2 aim, float range, float ppu)
    {
        if (!tuning.showSwipeArc) return;

        // The wedge is drawn at a fixed pixel radius and scaled, so it is only
        // regenerated when the arc angle itself changes.
        if (cached == null || !Mathf.Approximately(cachedArc, tuning.swipeArc))
        {
            cached = GreyboxArt.Wedge(128, tuning.swipeArc, new Color(1f, 0.95f, 0.7f, 0.5f), ppu);
            cachedArc = tuning.swipeArc;
        }

        // Rotate on the flat ground plane, then squash: that ordering is what the
        // isometric projection actually does, so the wedge lands where hits land.
        var root = new GameObject("swipe");
        root.transform.position = at;
        root.transform.localScale = new Vector3(1f, tuning.isoSquash, 1f);

        Vector2 flat = new Vector2(aim.x, aim.y / tuning.isoSquash);
        float deg = Mathf.Atan2(flat.y, flat.x) * Mathf.Rad2Deg;

        var child = new GameObject("arc");
        child.transform.SetParent(root.transform, false);
        child.transform.localRotation = Quaternion.Euler(0f, 0f, deg);
        child.transform.localScale = Vector3.one * (range / (128f / ppu));

        var sr = child.AddComponent<SpriteRenderer>();
        sr.sprite = cached;
        sr.sortingOrder = 2;

        var fx = root.AddComponent<SwipeFx>();
        fx.sr = sr;
        fx.life = 0.18f;
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
