using System.Collections.Generic;
using UnityEngine;

/// <summary>How a package names its direction folders and files.</summary>
public enum DirectionStyle
{
    /// <summary>south, southeast, east… — the Uries package.</summary>
    LongLower,

    /// <summary>s, se, e… — the Mech package.</summary>
    ShortLower,

    /// <summary>S, SE, E… — the FlyingTank package.</summary>
    ShortUpper,
}

/// <summary>
/// Everything needed to find one package's frames. Held as data rather than code so
/// a new delivery is a path template and a clip list, not another branch in here.
/// </summary>
public struct ArtConfig
{
    public string folder;          // Resources root, e.g. "FlyingTank"
    public string prefix;          // filename stem, e.g. "FlyingTank"
    public string pathFormat;      // see DirectionalArt.Load for the tokens
    public int frameDigits;
    public int firstFrame;
    public DirectionStyle style;

    /// <summary>Five rendered directions with the other three mirrored, or all eight.</summary>
    public bool mirrored;
}

/// <summary>
/// Loads directional sprite animations from Resources and builds the sprites in code.
///
/// Deliberately does not load Sprite assets, because that would depend on Unity
/// import settings being right on every machine. A PNG imported with default
/// settings is still a Texture2D, so pivot and pixels-per-unit come from the art
/// spec instead of from the .meta — a fresh clone works with no setup step.
///
/// Three packages have now arrived in three different naming conventions, so paths
/// are a template rather than a special case per character.
/// </summary>
public static class DirectionalArt
{
    public const float Fps = 12f;
    public const float PixelsPerUnit = 128f;

    /// <summary>Ground contact at 50% width, 88% height — the spec every package follows.</summary>
    public static readonly Vector2 Pivot = new(0.5f, 0.12f);

    /// <summary>Facing 0..7 is s, se, e, ne, n, nw, w, sw.</summary>
    static readonly int[] MirrorSource = { 0, 1, 2, 3, 4, 3, 2, 1 };
    static readonly bool[] MirrorFlip = { false, false, false, false, false, true, true, true };

    static readonly string[] LongLower =
        { "south", "southeast", "east", "northeast", "north", "northwest", "west", "southwest" };
    static readonly string[] ShortLower =
        { "s", "se", "e", "ne", "n", "nw", "w", "sw" };
    static readonly string[] ShortUpper =
        { "S", "SE", "E", "NE", "N", "NW", "W", "SW" };

    static readonly Dictionary<string, Sprite[]> cache = new();

    public static void ClearCache() => cache.Clear();

    /// <summary>Whether this facing is drawn by flipping a rendered one.</summary>
    public static bool IsFlipped(in ArtConfig cfg, int facing) =>
        cfg.mirrored && MirrorFlip[Mathf.Clamp(facing, 0, 7)];

    /// <summary>
    /// Loads one animation for one facing, or null when the art is absent.
    ///
    /// pathFormat tokens: {root} {prefix} {clip} {dir} {frame}
    ///   Uries       {root}/{clip}/{dir}/{prefix}_{clip}_{dir}_{frame}
    ///   Mech        {root}/{clip}/{prefix}_{clip}_{dir}_{frame}
    ///   FlyingTank  {root}/{dir}/{prefix}_{dir}_{clip}_{frame}
    ///   GroundTank  {root}/{dir}/{clip}/{prefix}_{dir}_{clip}_{frame}
    /// </summary>
    public static Sprite[] Load(in ArtConfig cfg, string clip, int facing, int frameCount)
    {
        facing = Mathf.Clamp(facing, 0, 7);
        int dirIndex = cfg.mirrored ? MirrorSource[facing] : facing;

        string dir = (cfg.style switch
        {
            DirectionStyle.LongLower => LongLower,
            DirectionStyle.ShortUpper => ShortUpper,
            _ => ShortLower,
        })[dirIndex];

        string key = $"{cfg.folder}|{cfg.prefix}|{clip}|{dir}|{cfg.pathFormat}|{cfg.firstFrame}|{cfg.frameDigits}|{frameCount}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        var frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            string number = (cfg.firstFrame + i).ToString(new string('0', cfg.frameDigits));

            string path = cfg.pathFormat
                .Replace("{root}", cfg.folder)
                .Replace("{prefix}", cfg.prefix)
                .Replace("{clip}", clip)
                .Replace("{dir}", dir)
                .Replace("{frame}", number);

            var tex = Resources.Load<Texture2D>(path);
            if (tex == null) { cache[key] = null; return null; }

            frames[i] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                      Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        cache[key] = frames;
        return frames;
    }
}
