using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Fades any building that is drawn in front of the player and overlapping them.
///
/// Sprite sorting is binary — the whole kaiju is either in front of a building or
/// behind it, and it flips the moment its feet cross the building's pivot. Nothing
/// short of per-pixel depth makes that gradual. This does not try to. It makes the
/// hidden state translucent instead of opaque, so the flip stops reading as the
/// player vanishing and the player is never lost behind a tower.
/// </summary>
public class OccluderFade : MonoBehaviour
{
    public static OccluderFade Instance { get; private set; }

    public Tuning tuning;
    public SpriteRenderer playerArt;

    readonly List<SpriteRenderer> occluders = new();

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void Register(SpriteRenderer sr) => occluders.Add(sr);

    /// <summary>Called when a building collapses — rubble can never occlude, so it stops being tracked.</summary>
    public void Unregister(SpriteRenderer sr)
    {
        if (occluders.Remove(sr) && sr != null)
        {
            var c = sr.color;
            c.a = 1f;
            sr.color = c;
        }
    }

    void LateUpdate()
    {
        if (playerArt == null) return;

        float step = Time.deltaTime / Mathf.Max(0.01f, tuning.occluderFadeTime);
        Bounds player = playerArt.bounds;
        float feetY = playerArt.transform.position.y;

        for (int i = 0; i < occluders.Count; i++)
        {
            var sr = occluders[i];
            if (sr == null) continue;

            // Drawn in front of the player, and actually covering them.
            //
            // Tested against an inset of the sprite's bounds, not the whole thing. A
            // building's canvas is mostly empty air — and the ones scaled up to fit a
            // 3x3 footprint are nine world units across — so a raw bounds test fades
            // half the street the moment you walk down it. Two buildings at 40% each
            // read as the city going see-through rather than as one thing getting out
            // of the way.
            var box = sr.bounds;
            float keep = 1f - tuning.occluderInset;
            box.extents = new Vector3(box.extents.x * keep, box.extents.y * keep, box.extents.z);

            bool covering = tuning.occluderFadeEnabled
                            && sr.transform.position.y < feetY
                            && box.Intersects(player);

            float target = covering ? tuning.occluderAlpha : 1f;
            var c = sr.color;
            if (Mathf.Approximately(c.a, target)) continue;

            c.a = Mathf.MoveTowards(c.a, target, step);
            sr.color = c;
        }
    }
}
