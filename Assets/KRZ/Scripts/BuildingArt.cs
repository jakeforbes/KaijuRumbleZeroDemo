using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the delivered building stills. Like the character art, sprites are built in
/// code from the raw texture rather than relying on import settings, so a fresh clone
/// needs no setup pass — and one cache entry serves every copy of a given type.
///
/// The art is authored at 256x128 pixels per ground tile, which at this project's
/// 128 PPU is exactly one world unit per half tile, so nothing is scaled.
///
/// Four damage states, named as the artist's manifest names them. Any state can be
/// missing: a type with only a pristine render darkens instead, and a type with no
/// render at all stays on greybox. That is what lets the roster be half illustrated
/// while the rest of the art is still being made.
/// </summary>
public static class BuildingArt
{
    public const int Pristine = 0, Damaged1 = 1, Damaged2 = 2, Destroyed = 3;
    public const int StageCount = 4;

    static readonly string[] Suffix = { "_pristine", "_damaged_1", "_damaged_2", "_destroyed" };

    static readonly Dictionary<(string, int, int), Sprite> Cache = new();

    /// <summary>
    /// Buildings pivot on the centre of their footprint, not on the art spec's ground
    /// contact point. The contact point is the diamond's southern corner — that is what
    /// 12%-from-the-bottom means for a building rather than a character — and the
    /// collider is built around the centre, so pivoting on the corner would leave every
    /// art building floating a full tile north of the thing you actually bump into.
    /// </summary>
    public static Vector2 Pivot(int frameHeightPx, int tilesX, int tilesY)
    {
        float contactFromBottom = GreyboxArt.FootPadding * frameHeightPx;
        float centreFromBottom = contactFromBottom + GreyboxArt.TileH * 0.25f * (tilesX + tilesY);
        return new Vector2(0.5f, centreFromBottom / frameHeightPx);
    }

    /// <summary>
    /// All four states for one type, any of which may be null. Index with the Pristine
    /// / Damaged1 / Damaged2 / Destroyed constants.
    /// </summary>
    public static Sprite[] LoadStages(string basePath, int tilesX, int tilesY, float ppu)
    {
        var stages = new Sprite[StageCount];
        if (string.IsNullOrEmpty(basePath)) return stages;

        for (int i = 0; i < StageCount; i++)
            stages[i] = Load(basePath + Suffix[i], tilesX, tilesY, ppu);

        // The first delivery shipped one unsuffixed file per building, before damage
        // states existed. Falling back to it keeps those four types working untouched.
        stages[Pristine] ??= Load(basePath, tilesX, tilesY, ppu);
        return stages;
    }

    /// <summary>Returns null for a blank path or a missing file, without complaint.</summary>
    public static Sprite Load(string resourcePath, int tilesX, int tilesY, float ppu)
    {
        if (string.IsNullOrEmpty(resourcePath)) return null;

        var key = (resourcePath, tilesX, tilesY);
        if (Cache.TryGetValue(key, out var cached)) return cached;

        var tex = Resources.Load<Texture2D>(resourcePath);
        Sprite sprite = null;

        if (tex != null)
        {
            sprite = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                   Pivot(tex.height, tilesX, tilesY),
                                   ppu, 0, SpriteMeshType.FullRect);
            sprite.name = resourcePath;
        }

        Cache[key] = sprite;
        return sprite;
    }
}
