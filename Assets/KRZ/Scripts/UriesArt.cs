using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads the artist's Uries frames and plays them.
///
/// Five directions are rendered; southwest, west and northwest come from flipping
/// their eastern counterparts, exactly as the art package intends. The five growth
/// levels are separate art rather than one sprite scaled, so the level swaps as the
/// kaiju grows and the engine scale only handles on-screen size.
/// </summary>
public class UriesArt : MonoBehaviour
{
    public enum Clip { Idle, Walk, Swipe, Blast, Hit }

    // Frame counts from the package README.
    static readonly Dictionary<Clip, int> FrameCounts = new()
    {
        { Clip.Idle, 4 }, { Clip.Walk, 8 }, { Clip.Swipe, 6 }, { Clip.Blast, 6 }, { Clip.Hit, 3 }
    };

    static readonly string[] ClipNames = { "idle", "walk", "swipe", "blast", "hit" };

    /// <summary>A clip the delivered art cannot supply, rebuilt from one that can.</summary>
    readonly struct Patch
    {
        public readonly Clip Source;
        public readonly int[] Frames;
        public readonly float Fps;

        public Patch(Clip source, int[] frames, float fps)
        {
            Source = source; Frames = frames; Fps = fps;
        }
    }

    /// <summary>
    /// Level 2's idle shipped broken in all five directions: frames 01 and 02 each
    /// contain two characters side by side — 420 px of occupied canvas against the
    /// 185 px the character actually fills — and frames 00 and 03 are crushed to
    /// half width. The loop was strobing between three silhouettes four times a
    /// second, which is the jankiness that was reported.
    ///
    /// Level 2's walk cycle is clean: all eight frames sit within 13 px of the same
    /// width with the feet on the same row. So idle borrows the stride's two passing
    /// poses — frames 0 and 4, where the legs are closest together — alternated
    /// slowly. Those two are the only choice that works from every angle: the other
    /// six read fine head-on, where the projection foreshortens the stride away, but
    /// in profile they are unmistakably a walk playing on the spot.
    ///
    /// Delete this entry the moment the artist redelivers level 2's idle. Nothing
    /// else depends on it, and every other level's idle measured clean.
    /// </summary>
    static readonly Dictionary<(int Level, Clip Clip), Patch> Patches = new()
    {
        { (2, Clip.Idle), new Patch(Clip.Walk, new[] { 0, 4 }, 2.5f) },
    };

    /// <summary>Playback rate for a clip. A patched clip sets its own.</summary>
    static float FpsFor(int level, Clip clip)
        => Patches.TryGetValue((level, clip), out var patch) ? patch.Fps : Fps;

    // Facing index 0..7 is s, se, e, ne, n, nw, w, sw. The last three are mirrors.
    static readonly string[] DirFolder =
        { "south", "southeast", "east", "northeast", "north", "northeast", "east", "southeast" };
    static readonly bool[] DirFlip =
        { false, false, false, false, false, true, true, true };

    public const float Fps = 12f;

    /// <summary>Sprites are 512 px tall; size 1 shows at 128 px, so the base is a quarter.</summary>
    public const float CanvasScale = 0.25f;

    /// <summary>From the art spec and the package manifest, not from import settings.</summary>
    public const float PixelsPerUnit = 128f;
    public static readonly Vector2 Pivot = new(0.5f, 0.12f);

    public SpriteRenderer target;
    public PlayerController player;

    static readonly Dictionary<string, Sprite[]> cache = new();

    Clip current = Clip.Idle;
    float clipStartedAt;
    bool oneShot;

    public static bool Available { get; private set; }
    public static UriesArt Instance { get; private set; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Awake()
    {
        Instance = this;

        // Drop anything cached from a previous play, so a run that started before
        // the textures were configured does not poison the next one with nulls.
        cache.Clear();

        // One probe tells us whether the art is present at all, so the build still
        // runs on greybox if the package has not been copied in.
        Available = Load(1, Clip.Idle, "south") != null;
        if (!Available) Debug.LogWarning("KRZ: Uries frames not found, staying on greybox.");
    }

    /// <summary>Plays a clip once, falling back to idle or walk when it finishes.</summary>
    public void PlayOnce(Clip clip)
    {
        // A clip already playing is left alone rather than restarted. Being hit twice
        // inside a quarter of a second would otherwise reset the recoil to frame 0
        // each time and the character would never move past its first pose.
        if (oneShot && current == clip &&
            Time.time - clipStartedAt < FrameCounts[clip] / Fps) return;

        current = clip;
        clipStartedAt = Time.time;
        oneShot = true;
    }

    void LateUpdate()
    {
        if (!Available || target == null || player == null) return;

        var progress = PlayerProgress.Instance;
        int level = Mathf.Clamp((progress != null ? progress.Tier : 0) + 1, 1, 5);

        int facing = Mathf.Clamp(player.Facing, 0, 7);
        var frames = Load(level, current, DirFolder[facing]);

        if (frames == null || frames.Length == 0)
        {
            // Fall back rather than blank the character.
            frames = Load(level, Clip.Idle, DirFolder[facing]);
            if (frames == null || frames.Length == 0) return;
        }

        float elapsed = Time.time - clipStartedAt;
        int index = Mathf.FloorToInt(elapsed * FpsFor(level, current));

        if (oneShot && index >= frames.Length)
        {
            // Hand back to whichever loop is actually correct now, rather than always
            // idle and then switching to walk on the same frame — two resets in one
            // frame showed up as a stutter at the end of every swipe.
            oneShot = false;
            current = player.IsMoving ? Clip.Walk : Clip.Idle;
            clipStartedAt = Time.time;
            index = 0;

            frames = Load(level, current, DirFolder[facing]);
            if (frames == null || frames.Length == 0) return;
        }

        if (!oneShot)
        {
            // Driven by input intent, not velocity. Being jostled by a crowd produced
            // velocity the player never asked for, oscillating across the threshold and
            // resetting the loop to frame 0 every time it crossed.
            var wanted = player.IsMoving ? Clip.Walk : Clip.Idle;
            if (wanted != current)
            {
                current = wanted;
                clipStartedAt = Time.time;
                index = 0;
                frames = Load(level, current, DirFolder[facing]);
                if (frames == null || frames.Length == 0) return;
            }
            index %= frames.Length;
        }

        target.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        target.flipX = DirFlip[facing];
    }

    /// <summary>
    /// Loads the raw textures and builds sprites in code, rather than loading
    /// Sprite assets that depend on Unity import settings being right.
    ///
    /// A PNG imported with default settings is still a Texture2D, so this works on
    /// a fresh clone with no menu item run and no per-machine setup — the same
    /// reason the rest of the project builds itself at Play instead of being wired
    /// in the Editor. Pivot and PPU come from the art spec, not from the .meta.
    /// </summary>
    static Sprite[] Load(int level, Clip clip, string direction)
    {
        string key = $"Uries/Level_{level}/{ClipNames[(int)clip]}/{direction}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        // A patched clip borrows frames from a sibling that loaded cleanly, so the
        // substitution is invisible to everything above this method.
        if (Patches.TryGetValue((level, clip), out var patch))
        {
            var source = Load(level, patch.Source, direction);
            if (source == null || source.Length == 0) { cache[key] = null; return null; }

            var borrowed = new Sprite[patch.Frames.Length];
            for (int i = 0; i < borrowed.Length; i++)
                borrowed[i] = source[Mathf.Clamp(patch.Frames[i], 0, source.Length - 1)];

            cache[key] = borrowed;
            return borrowed;
        }

        int count = FrameCounts[clip];
        var frames = new Sprite[count];

        for (int i = 0; i < count; i++)
        {
            string file = $"uries_l{level}_{ClipNames[(int)clip]}_{direction}_{i:00}";
            var tex = Resources.Load<Texture2D>($"{key}/{file}");
            if (tex == null) { cache[key] = null; return null; }

            frames[i] = Sprite.Create(tex,
                                      new Rect(0f, 0f, tex.width, tex.height),
                                      Pivot, PixelsPerUnit,
                                      0, SpriteMeshType.FullRect);
        }

        cache[key] = frames;
        return frames;
    }

    public static void ClearCache() => cache.Clear();
}
