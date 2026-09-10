using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads directional sprite animations from Resources and builds the sprites in
/// code.
///
/// Deliberately does not load Sprite assets, because that would depend on Unity
/// import settings being right on every machine. A PNG imported with default
/// settings is still a Texture2D, so pivot and pixels-per-unit come from the art
/// spec instead of from the .meta — a fresh clone works with no setup step.
///
/// Handles both delivered naming conventions: Uries ships long direction names and
/// two-digit frames from 00, the Mech ships short names and four-digit frames from
/// 0001. One loader rather than one per character.
/// </summary>
public static class DirectionalArt
{
    public const float Fps = 12f;
    public const float PixelsPerUnit = 128f;

    /// <summary>Ground contact at 50% width, 88% height — the spec every package follows.</summary>
    public static readonly Vector2 Pivot = new(0.5f, 0.12f);

    /// <summary>Facing 0..7 is s, se, e, ne, n, nw, w, sw. The last three are mirrors.</summary>
    public static readonly int[] SourceDirection = { 0, 1, 2, 3, 4, 3, 2, 1 };
    public static readonly bool[] Mirrored = { false, false, false, false, false, true, true, true };

    static readonly string[] LongNames = { "south", "southeast", "east", "northeast", "north" };
    static readonly string[] ShortNames = { "s", "se", "e", "ne", "n" };

    static readonly Dictionary<string, Sprite[]> cache = new();

    public static void ClearCache() => cache.Clear();

    /// <summary>
    /// Loads one animation for one facing, or null when the art is absent.
    /// <paramref name="folder"/> is the Resources path, e.g. "Mech" or "Uries/Level_1".
    /// <paramref name="prefix"/> is the filename stem, e.g. "mech" or "uries_l1".
    /// </summary>
    public static Sprite[] Load(string folder, string prefix, string clip, int facing,
                                int frameCount, bool longNames, int digits, int firstFrame)
    {
        int dir = SourceDirection[Mathf.Clamp(facing, 0, 7)];
        string dirName = (longNames ? LongNames : ShortNames)[dir];

        // Uries nests frames per direction; the Mech keeps one folder per animation.
        string path = longNames ? $"{folder}/{clip}/{dirName}" : $"{folder}/{clip}";
        string key = $"{path}|{prefix}|{clip}|{dirName}";

        if (cache.TryGetValue(key, out var cached)) return cached;

        var frames = new Sprite[frameCount];
        for (int i = 0; i < frameCount; i++)
        {
            string number = (firstFrame + i).ToString(new string('0', digits));
            var tex = Resources.Load<Texture2D>($"{path}/{prefix}_{clip}_{dirName}_{number}");
            if (tex == null) { cache[key] = null; return null; }

            frames[i] = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height),
                                      Pivot, PixelsPerUnit, 0, SpriteMeshType.FullRect);
        }

        cache[key] = frames;
        return frames;
    }
}
