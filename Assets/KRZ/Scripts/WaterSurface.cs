using UnityEngine;

/// <summary>
/// Cycles the ocean's frames. One renderer covering the whole sea rather than a tile
/// per diamond: SpriteRenderer's tiled draw mode repeats the texture across whatever
/// size it is given, so forty units of water in every direction costs a single object
/// instead of the ten thousand the ground lattice already spends.
/// </summary>
public class WaterSurface : MonoBehaviour
{
    public Sprite[] frames;
    public float fps = 7f;

    SpriteRenderer sr;
    int shown = -1;

    public static WaterSurface Attach(GameObject go, Sprite[] frames, Vector2 size, Color tint,
                                      int sortingOrder, float fps)
    {
        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = frames[0];
        sr.color = tint;
        sr.sortingOrder = sortingOrder;

        // Tiled needs a full-rect sprite and an explicit size; the sprite's own
        // pixels-per-unit then decides how many times the pattern repeats.
        sr.drawMode = SpriteDrawMode.Tiled;
        sr.tileMode = SpriteTileMode.Continuous;
        sr.size = size;

        var w = go.AddComponent<WaterSurface>();
        w.frames = frames;
        w.fps = fps;
        w.sr = sr;
        return w;
    }

    void Update()
    {
        if (frames == null || frames.Length == 0 || sr == null) return;

        int i = Mathf.FloorToInt(Time.time * fps) % frames.Length;
        if (i == shown) return;

        shown = i;

        // Tiled draw mode rebuilds its mesh when the sprite changes, so the size has
        // to be restated or the renderer collapses to the sprite's own bounds.
        var size = sr.size;
        sr.sprite = frames[i];
        sr.size = size;
    }
}
