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
    /// </summary>
    public static Vector2 Pivot(int frameHeightPx, int tilesX, int tilesY)
    {
        float centreFromBottom = GroundContactPx + GreyboxArt.TileH * 0.25f * (tilesX + tilesY);
        return new Vector2(0.5f, centreFromBottom / frameHeightPx);
    }

    /// <summary>
    /// All four states for one type in one rotation, any of which may be null. Index
    /// with the Pristine / Damaged1 / Damaged2 / Destroyed constants.
    /// </summary>
    public static Sprite[] LoadStages(string basePath, int direction, int tilesX, int tilesY, float ppu)
    {
        var stages = new Sprite[StageCount];
        if (string.IsNullOrEmpty(basePath)) return stages;

        string dir = Directions[Mathf.Clamp(direction, 0, DirectionCount - 1)];
        for (int i = 0; i < StageCount; i++)
            stages[i] = Load($"{basePath}_{Suffix[i]}_{dir}", tilesX, tilesY, ppu);

        return stages;
    }

    /// <summary>Returns null for a blank path or a missing file, without complaint.</summary>
    public static Sprite Load(string resourcePath, int tilesX, int tilesY, float ppu)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;

        var key = (resourcePath, tilesX, tilesY, ppu);
        if (Cache.TryGetValue(key, out var cached) && cached != null) return cached;

        var tex = Resources.Load<Texture2D>(resourcePath);
        Sprite sprite = null;

        if (tex != null)
        {
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                   Pivot(tex.height, tilesX, tilesY),
                                   ppu, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath;
        }

        // A missing or reimported texture must not poison subsequent spawns.
        if (sprite != null) Cache[key] = sprite;
        return sprite;
    }
}
