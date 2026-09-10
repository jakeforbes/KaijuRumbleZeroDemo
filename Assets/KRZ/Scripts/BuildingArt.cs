using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the delivered building stills. Like the character art, sprites are built in
/// code from the raw texture rather than relying on import settings, so a fresh clone
/// needs no setup pass — and one cache entry serves every copy of a given type.
///
/// Four damage states and five rotations per building. The rotation turns the model
/// on the spot without changing its ground diamond, which is what makes it free
/// variety: a city of two hundred and fifty buildings drawn from ten types repeats
/// itself badly, and five turns of each is most of the way out of that for nothing.
///
/// Any file can be missing. A type with only a pristine render darkens instead, and
/// a type with no render at all stays on greybox.
/// </summary>
public static class BuildingArt
{
    public const int Pristine = 0, Damaged1 = 1, Damaged2 = 2, Destroyed = 3;
    public const int StageCount = 4;

    /// <summary>Rotations the pack ships. The other three are these mirrored.</summary>
    public const int DirectionCount = 5;

    /// <summary>
    /// Where the model's ground contact sits above the bottom of its canvas, in
    /// pixels. The pack authors to a bottom-centre pivot with a small fixed margin
    /// rather than to a fraction of the frame, so this is a constant and not a ratio.
    /// Confirmed against the art: every building in the set bottoms out at 8.
    /// </summary>
    public const float GroundContactPx = 8f;

    static readonly string[] Suffix = { "pristine", "damaged_1", "damaged_2", "destroyed" };
    static readonly string[] Directions = { "s", "se", "e", "ne", "n" };

    static readonly Dictionary<(string, int, int, float), Sprite> Cache = new();

    public static void ClearCache() => Cache.Clear();

    /// <summary>
    /// Buildings pivot on the centre of their footprint, not on the artist's ground
    /// contact point. The contact point is the diamond's southern corner, and the
    /// collider is built around the centre, so pivoting on the corner would leave
    /// every building floating a tile north of the thing you actually bump into.
    ///
    /// The half-height is divided by artScale, and that division is the whole point.
    /// The sprite is drawn at ppu/artScale, so a distance measured in texture pixels
    /// shrinks by artScale on its way to the world. Using the unscaled half-height
    /// left every building floating above its diamond by (1 - artScale) of that
    /// half-height — fourteen percent of the footprint on the 1x1. Invisible on a
    /// tall building, whose mass anchors the eye, and glaring on rubble, which is
    /// nothing but its own base.
    ///
    /// <paramref name="offsetXPx"/> is for art whose base is not centred in its
    /// canvas. Two of the civilian sprites are drawn tens of pixels off centre, which
    /// no amount of scaling corrects — the building simply stands beside its collider.
    /// </summary>
    public static Vector2 Pivot(int frameWidthPx, int frameHeightPx, int tilesX, int tilesY,
                                float artScale, float offsetXPx, float offsetYPx)
    {
        float s = Mathf.Max(0.05f, artScale);
        float halfHeight = GreyboxArt.TileH * 0.25f * (tilesX + tilesY) / s;

        // Subtracting raises the drawn building: a lower pivot leaves less of the
        // sprite hanging below the transform.
        float centreFromBottom = GroundContactPx + halfHeight - offsetYPx;

        return new Vector2(0.5f + offsetXPx / frameWidthPx, centreFromBottom / frameHeightPx);
    }

    /// <summary>
    /// All four states for one type in one rotation, any of which may be null. Index
    /// with the Pristine / Damaged1 / Damaged2 / Destroyed constants.
    /// </summary>
    public static Sprite[] LoadStages(string basePath, int direction, int tilesX, int tilesY,
                                      float ppu, float artScale, float offsetXPx, float offsetYPx)
    {
        var stages = new Sprite[StageCount];
        if (string.IsNullOrEmpty(basePath)) return stages;

        string dir = Directions[Mathf.Clamp(direction, 0, DirectionCount - 1)];
        for (int i = 0; i < StageCount; i++)
            stages[i] = Load($"{basePath}_{Suffix[i]}_{dir}", tilesX, tilesY,
                             ppu, artScale, offsetXPx, offsetYPx);

        return stages;
    }

    /// <summary>Returns null for a blank path or a missing file, without complaint.</summary>
    public static Sprite Load(string resourcePath, int tilesX, int tilesY,
                              float ppu, float artScale, float offsetXPx, float offsetYPx)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;

        float s = Mathf.Max(0.05f, artScale);

        // Scale and offset both change the sprite, so both belong in the key.
        var key = (resourcePath, tilesX * 100 + tilesY, Mathf.RoundToInt(offsetXPx) * 4096 + Mathf.RoundToInt(offsetYPx), ppu / s);
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var tex = Resources.Load<Texture2D>(resourcePath);
        Sprite sprite = null;

        if (tex != null)
        {
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                   Pivot(tex.width, tex.height, tilesX, tilesY, s, offsetXPx, offsetYPx),
                                   ppu / s, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath;
        }

        // A missing or reimported texture must not poison subsequent spawns.
        if (sprite != null) Cache[key] = sprite;
        return sprite;
    }
}
