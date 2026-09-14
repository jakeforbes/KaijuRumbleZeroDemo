using UnityEngine;

/// <summary>
/// The way out of a level. Opens where the level's closing enemy fell and waits to be
/// walked into.
///
/// Deliberately something you step on rather than a banner that takes the screen. The
/// kill is the moment; the portal is the player choosing when they are done picking up
/// what dropped, which matters when the Lieutenant dies on top of a food scatter.
/// </summary>
public class Portal : MonoBehaviour
{
    static Portal instance;
    static Sprite sprite;

    /// <summary>True once a portal is on the map, so a second kill cannot open another.</summary>
    public static bool IsOpen => instance != null;

    public static void Reset()
    {
        instance = null;
        sprite = null;
    }

    static readonly Color Glow = new(0.72f, 0.45f, 1f);

    Tuning tuning;
    SpriteRenderer art;
    float radius;
    bool taken;

    /// <summary>Opens one at a point. Silently does nothing if one is already up.</summary>
    public static void Open(Tuning tuning, Vector3 at)
    {
        if (instance != null) return;
        if (sprite == null) sprite = GreyboxArt.Cloud(128, tuning.pixelsPerUnit);

        var go = new GameObject("Portal");
        go.transform.position = at;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = Glow;

        // Under everything that sorts by Y, like the toxin puffs — it lies on the ground
        // and should not draw over the kaiju standing in it.
        sr.sortingOrder = -38;

        var p = go.AddComponent<Portal>();
        p.tuning = tuning;
        p.art = sr;

        // Generous. A portal you have to stand exactly on is a portal you walk past twice
        // wondering whether it is broken.
        p.radius = 1.6f;
        go.transform.localScale = Vector3.one * (p.radius * 2f * tuning.pixelsPerUnit / 128f);

        instance = p;
        ShockwaveFx.Show(at, Glow, 1.2f, 0.5f, 0.6f);
        Popups.Add(at + Vector3.up * 2f, "<b>a way onward</b>", Glow);
    }

    void OnDestroy()
    {
        if (instance == this) instance = null;
    }

    void Update()
    {
        if (taken) return;

        var progress = PlayerProgress.Instance;
        if (progress == null || progress.RunOver) return;

        // Pulses so it reads as active rather than as scenery dropped on the street.
        float pulse = 0.55f + 0.3f * Mathf.Sin(Time.time * 3.2f);
        art.color = new Color(Glow.r, Glow.g, Glow.b, pulse);

        var player = GameBootstrap.Instance != null ? GameBootstrap.Instance.Player : null;
        if (player == null) return;

        Vector2 d = player.position - transform.position;
        var flat = new Vector2(d.x, d.y / tuning.isoSquash);
        if (flat.magnitude > radius) return;

        taken = true;
        PauseMenu.OpenEvolution();
    }
}
