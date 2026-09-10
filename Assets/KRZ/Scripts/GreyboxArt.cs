using UnityEngine;

/// <summary>
/// Generates placeholder sprites at runtime so nothing in the build waits on art.
/// Everything here uses the same pivot convention as the real sprites will:
/// ground contact at 50% width, and for characters 88% down the frame.
/// </summary>
public static class GreyboxArt
{
    public const int TileW = 256;   // isometric ground tile, 2:1
    public const int TileH = 128;

    /// <summary>Characters reserve this much of the frame below their feet, per the art spec.</summary>
    public const float FootPadding = 0.12f;

    /// <summary>
    /// The four ground corners of a w x h tile footprint, in pixels, relative to the
    /// footprint's centre. Both the sprite and the collider are built from this, so
    /// they cannot drift apart — art and collision disagreeing is what let the player
    /// walk onto building bases the first time round.
    /// Order is south, east, north, west; index 1 is always the lowest on screen.
    /// </summary>
    public static Vector2[] FootprintCornersPx(int tilesX, int tilesY)
    {
        // The two ground axes as they appear on screen in a 2:1 projection.
        var a = new Vector2(TileW * 0.5f, -TileH * 0.5f);
        var b = new Vector2(TileW * 0.5f, TileH * 0.5f);
        Vector2 half = (a * tilesX + b * tilesY) * 0.5f;

        return new[] { -half, a * tilesX - half, half, b * tilesY - half };
    }

    /// <summary>
    /// An isometric box on a w x h tile footprint: the ground face raised by heightPx,
    /// plus the two side faces facing the camera. Pivot sits at the footprint centre.
    /// </summary>
    public static Sprite IsoBox(int tilesX, int tilesY, int heightPx, Color top, float ppu)
    {
        var corners = FootprintCornersPx(tilesX, tilesY);

        float minX = float.MaxValue, maxX = float.MinValue;
        float minY = float.MaxValue, maxY = float.MinValue;
        foreach (var c in corners)
        {
            minX = Mathf.Min(minX, c.x); maxX = Mathf.Max(maxX, c.x);
            minY = Mathf.Min(minY, c.y); maxY = Mathf.Max(maxY, c.y);
        }

        int texW = Mathf.CeilToInt(maxX - minX);
        int texH = Mathf.CeilToInt(maxY - minY) + heightPx;
        float offX = -minX, offY = -minY;

        Color left = top * 0.62f;
        Color right = top * 0.42f;
        left.a = right.a = 1f;

        var lo = new float[texW];
        var hi = new float[texW];
        ColumnSpans(corners, texW, offX, offY, lo, hi);

        // Everything left of the nearest corner is the left face, everything right of
        // it the right face — that corner is where the two visible walls meet.
        float splitX = corners[1].x + offX;

        var px = new Color[texW * texH];
        for (int x = 0; x < texW; x++)
        {
            if (hi[x] < lo[x]) continue;
            float topLo = lo[x] + heightPx;
            float topHi = hi[x] + heightPx;
            bool isLeft = x + 0.5f < splitX;

            for (int y = 0; y < texH; y++)
            {
                float fy = y + 0.5f;
                if (fy >= topLo && fy <= topHi) px[y * texW + x] = top;
                else if (fy >= lo[x] && fy < topLo) px[y * texW + x] = isLeft ? left : right;
            }
        }

        Outline(px, texW, texH, 0.45f);
        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH),
                             new Vector2(offX / texW, offY / texH), ppu);
    }

    /// <summary>Per-column vertical extent of a convex polygon, used to fill the ground face.</summary>
    static void ColumnSpans(Vector2[] poly, int texW, float offX, float offY, float[] lo, float[] hi)
    {
        for (int x = 0; x < texW; x++) { lo[x] = float.MaxValue; hi[x] = float.MinValue; }

        for (int i = 0; i < poly.Length; i++)
        {
            Vector2 a = poly[i] + new Vector2(offX, offY);
            Vector2 b = poly[(i + 1) % poly.Length] + new Vector2(offX, offY);
            if (Mathf.Approximately(a.x, b.x)) continue;
            if (a.x > b.x) (a, b) = (b, a);

            int x0 = Mathf.Max(0, Mathf.CeilToInt(a.x - 0.5f));
            int x1 = Mathf.Min(texW - 1, Mathf.FloorToInt(b.x - 0.5f));
            for (int x = x0; x <= x1; x++)
            {
                float y = Mathf.Lerp(a.y, b.y, (x + 0.5f - a.x) / (b.x - a.x));
                if (y < lo[x]) lo[x] = y;
                if (y > hi[x]) hi[x] = y;
            }
        }
    }

    /// <summary>
    /// A capsule standing on the ground, used for the player and enemies.
    /// Frame height includes the 12% foot padding the real sprites will have.
    /// </summary>
    public static Sprite Capsule(int bodyW, int bodyH, Color body, float ppu)
    {
        int texW = bodyW;
        int texH = Mathf.RoundToInt(bodyH / (1f - FootPadding));
        int footY = texH - bodyH;   // pixel row the feet stand on

        var px = new Color[texW * texH];
        float r = bodyW * 0.5f;
        float cxp = texW * 0.5f;
        float capLo = footY + r;
        float capHi = footY + bodyH - r;

        for (int x = 0; x < texW; x++)
        {
            float ddx = x + 0.5f - cxp;
            for (int y = footY; y < texH; y++)
            {
                float fy = y + 0.5f;
                bool inside;
                if (fy < capLo) inside = ddx * ddx + (fy - capLo) * (fy - capLo) <= r * r;
                else if (fy > capHi) inside = ddx * ddx + (fy - capHi) * (fy - capHi) <= r * r;
                else inside = Mathf.Abs(ddx) <= r;

                if (inside) px[y * texW + x] = body;
            }
        }

        // A brighter cap so facing changes are visible before real art exists.
        for (int x = 0; x < texW; x++)
            for (int y = texH - Mathf.RoundToInt(bodyH * 0.22f); y < texH; y++)
                if (px[y * texW + x].a > 0f) px[y * texW + x] = body * 1.35f;

        Outline(px, texW, texH, 0.35f);
        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH),
                             new Vector2(0.5f, FootPadding), ppu);
    }

    /// <summary>Flat ground tile. Pivot at its centre.</summary>
    public static Sprite GroundTile(Color fill, Color edge, float ppu)
    {
        int texW = TileW, texH = TileH;
        var px = new Color[texW * texH];
        float cx = texW * 0.5f, cy = texH * 0.5f;

        for (int x = 0; x < texW; x++)
        {
            float nx = Mathf.Abs(x + 0.5f - cx) / (texW * 0.5f);
            if (nx > 1f) continue;
            float dy = (texH * 0.5f) * (1f - nx);
            for (int y = 0; y < texH; y++)
            {
                float fy = y + 0.5f;
                if (fy < cy - dy || fy > cy + dy) continue;
                float edgeDist = Mathf.Min(cy + dy - fy, fy - (cy - dy));
                px[y * texW + x] = edgeDist < 2.5f ? edge : fill;
            }
        }

        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), ppu);
    }

    /// <summary>Small diamond pickup. Pivot at the ground so it sorts with everything else.</summary>
    public static Sprite Pickup(int size, Color fill, float ppu)
    {
        int texW = size, texH = Mathf.Max(4, size / 2);
        var px = new Color[texW * texH];
        float cx = texW * 0.5f, cy = texH * 0.5f;

        for (int x = 0; x < texW; x++)
        {
            float nx = Mathf.Abs(x + 0.5f - cx) / (texW * 0.5f);
            if (nx > 1f) continue;
            float dy = (texH * 0.5f) * (1f - nx);
            for (int y = 0; y < texH; y++)
            {
                float fy = y + 0.5f;
                if (fy >= cy - dy && fy <= cy + dy) px[y * texW + x] = fill;
            }
        }

        Outline(px, texW, texH, 0.4f);
        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), ppu);
    }

    /// <summary>
    /// Filled wedge pointing along +X, pivot at its apex. Used to show the swipe arc
    /// while tuning range and width. Squash it vertically to lay it on the ground.
    /// </summary>
    public static Sprite Wedge(int radiusPx, float arcDegrees, Color fill, float ppu)
    {
        int size = radiusPx * 2;
        var px = new Color[size * size];
        float c = size * 0.5f;
        float half = arcDegrees * 0.5f * Mathf.Deg2Rad;

        for (int x = 0; x < size; x++)
            for (int y = 0; y < size; y++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > radiusPx || d < radiusPx * 0.18f) continue;
                if (Mathf.Abs(Mathf.Atan2(dy, dx)) > half) continue;

                var col = fill;
                col.a = fill.a * (1f - d / radiusPx) * 1.6f;   // brightest near the kaiju
                px[y * size + x] = col;
            }

        var tex = MakeTexture(px, size, size);
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), ppu);
    }

    /// <summary>
    /// Flat rectangle. pivotX 0 anchors it to its left edge, so scaling X makes a
    /// meter fill from the left instead of from the middle.
    /// </summary>
    public static Sprite Solid(int w, int h, Color fill, float ppu, float pivotX = 0.5f)
    {
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = fill;
        var tex = MakeTexture(px, w, h);
        return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(pivotX, 0.5f), ppu);
    }

    /// <summary>Soft elliptical contact shadow, drawn by the engine rather than baked into art.</summary>
    /// <summary>
    /// A stacked burger, read from the side rather than in projection. Everything else
    /// on the ground is an isometric diamond, so breaking the projection is exactly
    /// what makes this one findable across a district — it is the only thing on the
    /// map that does not look like it belongs to the city.
    /// </summary>
    public static Sprite Burger(int size, float ppu)
    {
        int texW = Mathf.Max(8, size), texH = Mathf.Max(6, size * 3 / 4);
        var px = new Color[texW * texH];

        // Bottom bun, patty, lettuce, top bun — as fractions of the height, bottom up.
        var bands = new (float from, float to, Color colour)[]
        {
            (0.00f, 0.26f, new Color(0.78f, 0.53f, 0.26f)),
            (0.26f, 0.44f, new Color(0.36f, 0.20f, 0.13f)),
            (0.44f, 0.56f, new Color(0.48f, 0.72f, 0.30f)),
            (0.56f, 1.00f, new Color(0.93f, 0.70f, 0.34f)),
        };

        for (int y = 0; y < texH; y++)
        {
            float ny = (y + 0.5f) / texH;

            // The top bun domes and the bottom bun tucks in, so the silhouette reads
            // as a burger rather than as a stack of bars.
            float half = 0.5f;
            if (ny > 0.56f) half = 0.5f * Mathf.Sqrt(Mathf.Max(0f, 1f - Mathf.Pow((ny - 0.56f) / 0.44f, 2f)));
            else if (ny < 0.26f) half = 0.5f * (0.72f + 0.28f * (ny / 0.26f));

            Color band = bands[0].colour;
            foreach (var b in bands) if (ny >= b.from && ny < b.to) band = b.colour;

            for (int x = 0; x < texW; x++)
            {
                float nx = (x + 0.5f) / texW - 0.5f;
                if (Mathf.Abs(nx) <= half) px[y * texW + x] = band;
            }
        }

        Outline(px, texW, texH, 0.45f);
        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.25f), ppu);
    }

    /// <summary>
    /// A soft 2:1 ellipse in white, so a SpriteRenderer tint decides the colour.
    /// Shadow is the same shape but its pixels are black, and black multiplied by any
    /// tint is still black — a coloured cloud has to start white.
    ///
    /// Falls off on a curve rather than linearly, which keeps a dense middle and a
    /// vague edge instead of reading as a flat disc.
    /// </summary>
    public static Sprite Cloud(int w, float ppu)
    {
        int texW = w, texH = Mathf.Max(4, w / 2);
        var px = new Color[texW * texH];
        float cx = texW * 0.5f, cy = texH * 0.5f;

        for (int x = 0; x < texW; x++)
            for (int y = 0; y < texH; y++)
            {
                float nx = (x + 0.5f - cx) / (texW * 0.5f);
                float ny = (y + 0.5f - cy) / (texH * 0.5f);
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d > 1f) continue;

                float a = 1f - d;
                px[y * texW + x] = new Color(1f, 1f, 1f, a * a * 0.85f + a * 0.15f);
            }

        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), ppu);
    }

    public static Sprite Shadow(int w, float ppu)
    {
        int texW = w, texH = Mathf.Max(4, w / 2);
        var px = new Color[texW * texH];
        float cx = texW * 0.5f, cy = texH * 0.5f;

        for (int x = 0; x < texW; x++)
            for (int y = 0; y < texH; y++)
            {
                float nx = (x + 0.5f - cx) / (texW * 0.5f);
                float ny = (y + 0.5f - cy) / (texH * 0.5f);
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                if (d <= 1f) px[y * texW + x] = new Color(0f, 0f, 0f, (1f - d) * 0.55f);
            }

        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH), new Vector2(0.5f, 0.5f), ppu);
    }

    // --- helpers -------------------------------------------------------

    /// <summary>Darkens pixels that sit against transparency, so greybox shapes read against each other.</summary>
    static void Outline(Color[] px, int w, int h, float strength)
    {
        var copy = (Color[])px.Clone();
        for (int x = 0; x < w; x++)
            for (int y = 0; y < h; y++)
            {
                int i = y * w + x;
                if (copy[i].a <= 0f) continue;
                bool edge =
                    x == 0 || x == w - 1 || y == 0 || y == h - 1 ||
                    copy[i - 1].a <= 0f || copy[i + 1].a <= 0f ||
                    copy[i - w].a <= 0f || copy[i + w].a <= 0f;
                if (edge)
                {
                    var c = copy[i] * (1f - strength);
                    c.a = 1f;
                    px[i] = c;
                }
            }
    }

    static Texture2D MakeTexture(Color[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }
}
