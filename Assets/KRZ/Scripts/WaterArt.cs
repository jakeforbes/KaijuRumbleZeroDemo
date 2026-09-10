using UnityEngine;

/// <summary>
/// Builds the ocean's animated tile in code, the way every other greybox surface in
/// this project is built. Generated rather than authored for two reasons: it costs
/// no art time on the last day, and a loop that is computed can be made to close
/// exactly, which a hand-drawn one usually cannot.
///
/// The look is blobs of blue separated by a pale caustic web, with sparkles at the
/// junctions. Blobs drift on small circular orbits, each with its own phase, so the
/// web between them opens and closes — the whole ripple is that one motion, and it
/// returns precisely to frame zero because every orbit completes one turn.
///
/// Authored at a quarter resolution and point-upscaled, which is what makes it read
/// as chunky pixel water rather than as a soft gradient.
/// </summary>
public static class WaterArt
{
    /// <summary>Cell centres wrap, so the tile repeats without a visible seam.</summary>
    public static Sprite[] Build(int frameCount, int sizePx, int grid, int chunk, float ppu)
    {
        frameCount = Mathf.Max(1, frameCount);
        chunk = Mathf.Max(1, chunk);

        int lo = Mathf.Max(8, sizePx / chunk);          // resolution the pattern is drawn at
        float cell = lo / (float)Mathf.Max(2, grid);

        var rng = new System.Random(20260910);
        int n = grid * grid;
        var baseX = new float[n];
        var baseY = new float[n];
        var phase = new float[n];
        var shade = new float[n];

        for (int i = 0; i < n; i++)
        {
            int gx = i % grid, gy = i / grid;
            baseX[i] = (gx + 0.2f + 0.6f * (float)rng.NextDouble()) * cell;
            baseY[i] = (gy + 0.2f + 0.6f * (float)rng.NextDouble()) * cell;
            phase[i] = (float)rng.NextDouble() * Mathf.PI * 2f;
            shade[i] = (float)rng.NextDouble();
        }

        // Bright palette. Deep water and shallows are the same sprite at different
        // renderer tints, so this is authored light enough to be darkened.
        var web = new Color(0.72f, 0.90f, 0.99f);
        var blobLight = new Color(0.47f, 0.64f, 0.90f);
        var blobDark = new Color(0.33f, 0.50f, 0.83f);

        float orbit = cell * 0.18f;
        var frames = new Sprite[frameCount];
        var small = new Color[lo * lo];

        for (int f = 0; f < frameCount; f++)
        {
            float t = f / (float)frameCount * Mathf.PI * 2f;

            for (int y = 0; y < lo; y++)
                for (int x = 0; x < lo; x++)
                {
                    int cx = Mathf.FloorToInt(x / cell);
                    int cy = Mathf.FloorToInt(y / cell);

                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    int nearest = 0;

                    // Only the three by three neighbourhood can hold the two closest
                    // centres, so this stays linear in pixels rather than in cells.
                    for (int oy = -1; oy <= 1; oy++)
                        for (int ox = -1; ox <= 1; ox++)
                        {
                            int gx = ((cx + ox) % grid + grid) % grid;
                            int gy = ((cy + oy) % grid + grid) % grid;
                            int i = gy * grid + gx;

                            float sx = baseX[i] + Mathf.Cos(t + phase[i]) * orbit;
                            float sy = baseY[i] + Mathf.Sin(t + phase[i]) * orbit;

                            float dx = Wrap(sx - x, lo);
                            float dy = Wrap(sy - y, lo);
                            float d = Mathf.Sqrt(dx * dx + dy * dy);

                            if (d < d1) { d2 = d1; d1 = d; nearest = i; }
                            else if (d < d2) d2 = d;
                        }

                    // Inside a blob, in its skirt, or out in the web between them.
                    float blobEdge = cell * 0.62f;
                    Color c;

                    if (d1 < blobEdge)
                    {
                        c = shade[nearest] < 0.45f ? blobDark : blobLight;

                        // A darker core in some blobs, which is what stops a field of
                        // flat shapes reading as a tiled pattern.
                        if (shade[nearest] > 0.72f && d1 < blobEdge * 0.55f) c = blobDark;
                    }
                    else
                    {
                        c = web;

                        // Sparkle where three cells meet: the two nearest centres are
                        // equidistant and both far, which only happens at a junction.
                        if (d2 - d1 < cell * 0.08f && ((x * 7 + y * 13 + f * 3) % 11) == 0)
                            c = Color.white;
                    }

                    small[y * lo + x] = c;
                }

            frames[f] = Upscale(small, lo, chunk, ppu);
        }

        return frames;
    }

    /// <summary>Shortest signed distance on a wrapping axis, so the tile has no seam.</summary>
    static float Wrap(float d, int span)
    {
        if (d > span * 0.5f) d -= span;
        else if (d < -span * 0.5f) d += span;
        return d;
    }

    static Sprite Upscale(Color[] small, int lo, int chunk, float ppu)
    {
        int size = lo * chunk;
        var px = new Color[size * size];

        for (int y = 0; y < size; y++)
        {
            int sy = y / chunk;
            for (int x = 0; x < size; x++)
                px[y * size + x] = small[sy * lo + x / chunk];
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
}
