using UnityEngine;

/// <summary>
/// The city floor: roads, kerbs and lots, drawn as isometric diamonds that match the
/// ground lattice exactly.
///
/// Everything is painted in tile-local coordinates and then projected, rather than
/// drawn as screen-space shapes that happen to look isometric. A road stripe is a
/// straight line across the square tile; the diamond is what the projection makes of
/// it. That is the only way markings stay parallel from tile to tile, and it means a
/// road running north reads as genuinely perpendicular to one running east.
///
/// Generated rather than imported because the delivered ground tiles are labelled
/// presentation swatches — a 147 pixel diamond inside a 256x128 frame with the word
/// ASPHALT under it — not tiles. Swapping real ones in later is a sprite assignment.
/// </summary>
public static class CityGroundArt
{
    // Read off the delivered building art so the floor belongs to the same city.
    static readonly Color Asphalt = new(0.155f, 0.175f, 0.235f);
    static readonly Color AsphaltAlt = new(0.135f, 0.155f, 0.210f);
    static readonly Color Seam = new(0.205f, 0.235f, 0.300f);
    static readonly Color Road = new(0.115f, 0.125f, 0.170f);
    static readonly Color Kerb = new(0.255f, 0.285f, 0.355f);
    static readonly Color Marking = new(0.70f, 0.745f, 0.83f);
    static readonly Color Accent = new(0.20f, 0.42f, 0.52f);

    public enum Kind { Lot, RoadU, RoadV, Crossing, Kerb }

    /// <summary>
    /// A seamless concrete tile in white, so the renderer's tint sets the colour.
    ///
    /// Three scales of variation and nothing else. No lines, no markings, no edges —
    /// those are what made the last floor unreadable. This has to survive being looked
    /// straight through, so every value stays within a few percent of flat: broad
    /// patches where a slab was poured differently, a fine per-pixel grain, and the
    /// occasional darker chip of aggregate.
    ///
    /// Generated at a fraction of its resolution and point-upscaled, which is what
    /// makes the grain land on a chunky pixel grid rather than dissolving into noise
    /// at the size it is actually drawn.
    /// </summary>
    public static Sprite Concrete(int sizePx, int chunk, float grain, float patch, float ppu)
    {
        chunk = Mathf.Max(1, chunk);
        int lo = Mathf.Max(8, sizePx / chunk);

        // Broad patches, on a wrapping lattice so the tile has no seam.
        const int Lattice = 4;
        var rng = new System.Random(5150);
        var lat = new float[Lattice, Lattice];
        for (int y = 0; y < Lattice; y++)
            for (int x = 0; x < Lattice; x++)
                lat[x, y] = (float)rng.NextDouble() * 2f - 1f;

        var small = new Color[lo * lo];

        for (int y = 0; y < lo; y++)
            for (int x = 0; x < lo; x++)
            {
                float v = 1f + Patch(lat, Lattice, x / (float)lo, y / (float)lo) * patch;

                // Fine grain: uncorrelated by design, which also tiles for free.
                v += (Hash(x, y, 1) - 0.5f) * 2f * grain;

                // Aggregate. Rare, and dark far more often than light, because a chip
                // in concrete is a shadow rather than a highlight.
                float chip = Hash(x, y, 2);
                if (chip > 0.982f) v += grain * 3.5f;
                else if (chip < 0.028f) v -= grain * 4.5f;

                v = Mathf.Clamp(v, 0.6f, 1.4f);
                small[y * lo + x] = new Color(v, v, v, 1f);
            }

        int size = lo * chunk;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
        {
            int sy = y / chunk;
            for (int x = 0; x < size; x++) px[y * size + x] = small[sy * lo + x / chunk];
        }

        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Point,
            wrapMode = TextureWrapMode.Repeat,
        };
        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f),
                             ppu, 0, SpriteMeshType.FullRect);
    }

    /// <summary>Smoothed value noise on a wrapping lattice.</summary>
    static float Patch(float[,] lat, int n, float u, float v)
    {
        float fx = u * n, fy = v * n;
        int x0 = Mathf.FloorToInt(fx) % n, y0 = Mathf.FloorToInt(fy) % n;
        int x1 = (x0 + 1) % n, y1 = (y0 + 1) % n;

        float tx = fx - Mathf.Floor(fx), ty = fy - Mathf.Floor(fy);
        tx = tx * tx * (3f - 2f * tx);
        ty = ty * ty * (3f - 2f * ty);

        return Mathf.Lerp(Mathf.Lerp(lat[x0, y0], lat[x1, y0], tx),
                          Mathf.Lerp(lat[x0, y1], lat[x1, y1], tx), ty);
    }

    static float Hash(int x, int y, int salt)
    {
        int h = x * 374761393 + y * 668265263 + salt * 1442695040;
        h = (h ^ (h >> 13)) * 1274126177;
        return ((h ^ (h >> 16)) & 0xFFFFFF) / (float)0xFFFFFF;
    }

    /// <summary>
    /// One tile. <paramref name="variant"/> only matters for lots, where it picks
    /// between plain tarmac, parking bays, a patched surface and a hatched yard — the
    /// point being that open ground looks like somewhere, rather than like a hole
    /// where a building failed to spawn.
    /// </summary>
    public static Sprite Tile(Kind kind, int variant, float ppu)
    {
        int w = GreyboxArt.TileW, h = GreyboxArt.TileH;
        var px = new Color[w * h];

        float halfW = w * 0.5f, halfH = h * 0.5f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                // Screen offset from the diamond's centre, normalised to its half axes.
                float sx = (x + 0.5f - halfW) / halfW;
                float sy = (y + 0.5f - halfH) / halfH;

                // Back into the square tile the diamond is a projection of.
                float u = (sx + sy + 1f) * 0.5f;
                float v = (sy - sx + 1f) * 0.5f;
                if (u < 0f || u > 1f || v < 0f || v > 1f) continue;

                px[y * w + x] = Paint(kind, variant, u, v);
            }

        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        tex.SetPixels(px);
        tex.Apply();

        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f),
                             ppu, 0, SpriteMeshType.FullRect);
    }

    static Color Paint(Kind kind, int variant, float u, float v)
    {
        // Distance to the nearest tile edge, for kerbs and seams.
        float edge = Mathf.Min(Mathf.Min(u, 1f - u), Mathf.Min(v, 1f - v));

        switch (kind)
        {
            case Kind.RoadU: return PaintRoad(u, v, edge);
            case Kind.RoadV: return PaintRoad(v, u, edge);

            case Kind.Crossing:
            {
                var c = Road;
                // Stop lines at all four mouths rather than a centre line, which is
                // what makes a junction read as a junction from above.
                if (edge > 0.12f && edge < 0.17f) c = Marking;
                return c;
            }

            case Kind.Kerb:
            {
                var c = Asphalt;
                if (edge < 0.10f) c = Kerb;
                return c;
            }

            default: return PaintLot(variant, u, v, edge);
        }
    }

    /// <summary>
    /// A road running along the first axis. The centre line dashes along it and the
    /// gutters sit against the two edges it runs between.
    /// </summary>
    static Color PaintRoad(float along, float across, float edge)
    {
        var c = Road;

        // Gutters down both sides of the carriageway.
        if (across < 0.08f || across > 0.92f) return Kerb;

        // Dashed centre line: on for six tenths of each half-tile, off for the rest.
        if (Mathf.Abs(across - 0.5f) < 0.022f && Mathf.Repeat(along * 4f, 1f) < 0.55f)
            c = Marking;

        return c;
    }

    static Color PaintLot(int variant, float u, float v, float edge)
    {
        var c = ((int)(u * 4f) + (int)(v * 4f)) % 2 == 0 ? Asphalt : AsphaltAlt;

        // Panel seams on a four by four grid, so the ground has a sense of scale
        // under a kaiju that grows fourfold across a run.
        if (Mathf.Repeat(u * 4f, 1f) < 0.035f || Mathf.Repeat(v * 4f, 1f) < 0.035f) c = Seam;
        if (edge < 0.035f) c = Seam;

        switch (variant)
        {
            case 1:   // parking bays
                if (v > 0.18f && v < 0.82f && Mathf.Repeat(u * 6f, 1f) < 0.05f) c = Marking * 0.75f;
                if (Mathf.Abs(v - 0.5f) < 0.02f) c = Marking * 0.55f;
                break;

            case 2:   // patched and resurfaced
                float patch = Mathf.PerlinNoise(u * 3.1f + 11.3f, v * 3.1f + 4.7f);
                if (patch > 0.62f) c = Color.Lerp(c, Road, 0.7f);
                break;

            case 3:   // hatched service yard
                if (Mathf.Repeat((u + v) * 7f, 1f) < 0.14f) c = Color.Lerp(c, Accent, 0.5f);
                if (edge < 0.07f) c = Kerb;
                break;
        }

        return c;
    }
}
