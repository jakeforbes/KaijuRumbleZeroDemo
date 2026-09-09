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
    /// An isometric box: diamond top face plus two shaded side faces.
    /// Pivot sits at the centre of the base diamond, so it sorts and collides correctly.
    /// </summary>
    public static Sprite IsoBox(int tiles, int heightPx, Color top, float ppu)
    {
        int dW = TileW * tiles;
        int dH = TileH * tiles;
        int texW = dW;
        int texH = dH + heightPx;

        Color left = top * 0.62f;
        Color right = top * 0.42f;
        left.a = right.a = 1f;

        var px = new Color[texW * texH];
        float cx = dW * 0.5f;
        float cyBase = dH * 0.5f;
        float cyTop = cyBase + heightPx;

        for (int x = 0; x < texW; x++)
        {
            float nx = Mathf.Abs(x + 0.5f - cx) / (dW * 0.5f);
            if (nx > 1f) continue;

            float dy = (dH * 0.5f) * (1f - nx);
            float topLo = cyTop - dy;
            float topHi = cyTop + dy;
            float sideLo = cyBase - dy;
            bool isLeft = x + 0.5f < cx;

            for (int y = 0; y < texH; y++)
            {
                float fy = y + 0.5f;
                if (fy >= topLo && fy <= topHi) px[y * texW + x] = top;
                else if (fy >= sideLo && fy < topLo) px[y * texW + x] = isLeft ? left : right;
            }
        }

        Outline(px, texW, texH, 0.45f);
        var tex = MakeTexture(px, texW, texH);
        return Sprite.Create(tex, new Rect(0, 0, texW, texH),
                             new Vector2(0.5f, cyBase / texH), ppu);
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
