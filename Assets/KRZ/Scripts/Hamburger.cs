using UnityEngine;

/// <summary>
/// The hamburger. One per quadrant, and unlike every other pickup it is a one-shot
/// consumable rather than a stacking upgrade.
///
/// Walking into it hauls every food gem within a wide radius straight to you. That
/// is deliberately the loudest thing in the game for two seconds — the payoff is
/// watching a quarter of a district's worth of gems converge at once, which is the
/// reward for having gone out of your way to a corner of the map.
///
/// It pulls from a fraction of the map rather than all of it, so it stays a reason
/// to smash a district first and then collect, instead of a button that ends the
/// food economy.
/// </summary>
public class Hamburger : MonoBehaviour
{
    static Sprite sprite;
    static Transform root;

    Tuning tuning;
    float radius;
    float bobPhase;
    Vector3 basePos;

    public static void Reset() => root = null;

    public static void Spawn(Tuning tuning, Vector3 at, float radius, float ppu)
    {
        if (root == null) root = new GameObject("Hamburgers").transform;
        if (sprite == null) sprite = GreyboxArt.Burger(tuning.hamburgerPx, ppu);

        var go = new GameObject("Hamburger");
        go.transform.SetParent(root, false);
        go.transform.position = at;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.sortingOrder = 2;   // above food, so it never hides under gems

        var h = go.AddComponent<Hamburger>();
        h.tuning = tuning;
        h.radius = radius;
        h.basePos = at;
        h.bobPhase = Random.value * 10f;
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null) return;

        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.2f + bobPhase) * 0.16f);

        Vector2 d = progress.transform.position - basePos;
        float flat = new Vector2(d.x, d.y / tuning.isoSquash).magnitude;
        if (flat > tuning.upgradePickupRange * progress.Scale) return;

        Consume(progress);
    }

    void Consume(PlayerProgress progress)
    {
        Vector2 origin = progress.transform.position;

        foreach (var f in Food.All)
        {
            if (f == null) continue;

            Vector2 delta = (Vector2)f.transform.position - origin;
            if (new Vector2(delta.x, delta.y / tuning.isoSquash).magnitude > radius) continue;

            f.Attract(tuning.hamburgerAttractSeconds);
        }

        AudioEvents.Play(Sfx.UpgradePickup, transform.position, owner: gameObject);

        Destroy(gameObject);
    }
}
