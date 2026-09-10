using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the delivered building stills. Like the character art, sprites are built in
/// code from the raw texture rather than relying on import settings, so a fresh clone
/// needs no setup pass — and one cache entry serves every copy of a given type.
///
/// The art is authored at 256x128 pixels per ground tile, which at this project's
/// 128 PPU is exactly one world unit per half tile, so nothing is scaled.
/// </summary>
public static class BuildingArt
{
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

    /// <summary>Returns null for a blank path or a missing file, leaving the type on greybox.</summary>
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
        else
        {
            Debug.LogWarning($"BuildingArt: no texture at Resources/{resourcePath}");
        }

        Cache[key] = sprite;
        return sprite;
    }
}
