using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Loads a character's frames and plays them. Was UriesArt, which hardcoded one package's
/// paths; CharacterType.Roster now supplies them, so the Alien, the Lizard and the Skeleton
/// all run through here unchanged.
///
/// Five directions are rendered; southwest, west and northwest come from flipping their
/// eastern counterparts, exactly as the art intends. Whether the five growth tiers are
/// separate art or one set scaled up is the character's business — see CharacterType.artFolder.
/// </summary>
public class CharacterArt : MonoBehaviour
{
    public enum Clip { Idle, Walk, Swipe, Blast, Hit }

    static readonly string[] ClipNames = { "idle", "walk", "swipe", "blast", "hit" };

    // Facing index 0..7 is s, se, e, ne, n, nw, w, sw. The last three are mirrors.
    static readonly string[] DirFolder =
        { "south", "southeast", "east", "northeast", "north", "northeast", "east", "southeast" };
    static readonly bool[] DirFlip =
        { false, false, false, false, false, true, true, true };

    public const float Fps = 12f;

    /// <summary>Sprites are 512 px tall; size 1 shows at 128 px, so the base is a quarter.</summary>
    public const float CanvasScale = 0.25f;

    /// <summary>From the art spec, not from import settings.</summary>
    public const float PixelsPerUnit = 128f;
    public static readonly Vector2 Pivot = new(0.5f, 0.12f);

    /// <summary>Frames the patched level 2 idle borrows from the walk cycle, and its rate.</summary>
    static readonly int[] Level2IdleFrames = { 0, 4 };
    const float Level2IdleFps = 2.5f;

    /// <summary>
    /// Which character the next run uses, as an index into CharacterType.Roster.
    ///
    /// Deliberately outside Reset(): the character select sets it before Restart reloads
    /// the scene, and Reset runs after, so clearing it there would throw the choice away
    /// and drop every restart back onto the Alien.
    /// </summary>
    public static int SelectedIndex { get; set; }

    public SpriteRenderer target;
    public PlayerController player;

    /// <summary>Kept so an unavailable character can fall back to what GameBootstrap built.</summary>
    public Sprite greybox;

    static readonly Dictionary<string, Sprite[]> cache = new();

    Tuning tuning;
    CharacterType character;
    Clip current = Clip.Idle;
    float clipStartedAt;
    bool oneShot;

    /// <summary>
    /// Whether Apply has run with a renderer to write to. AddComponent calls Awake before
    /// GameBootstrap gets to assign target, so the first real apply has to wait for the
    /// first LateUpdate — without this flag it would be skipped entirely, because by then
    /// the resolved character already matches and nothing looks like it changed.
    /// </summary>
    bool applied;

    public static bool Available { get; private set; }
    public static CharacterArt Instance { get; private set; }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Awake()
    {
        Instance = this;
        tuning = Resources.Load<Tuning>("Tuning");

        // Drop anything cached from a previous play, so a run that started before the
        // textures were configured does not poison the next one with nulls.
        cache.Clear();
    }

    /// <summary>
    /// The roster entry the run is using, or the first one if the index has drifted out of
    /// range. Static and computed rather than cached, so it answers correctly before any
    /// CharacterArt exists — PlayerSpecial needs it to know which special to fire, and its
    /// Awake runs while the player is still being assembled.
    /// </summary>
    public static CharacterType Selected
    {
        get
        {
            var roster = CharacterType.Roster;
            if (roster == null || roster.Length == 0) return null;
            return roster[Mathf.Clamp(SelectedIndex, 0, roster.Length - 1)];
        }
    }

    CharacterType Resolve() => Selected;

    /// <summary>
    /// Points the renderer at a character. Colour and scale are set here rather than every
    /// frame on purpose: PlayerProgress flashes the same SpriteRenderer on damage, and a
    /// per-frame write would overwrite the flash before it was ever visible.
    /// </summary>
    void Apply(CharacterType c)
    {
        character = c;
        var probe = c != null ? Load(c, 1, Clip.Idle, "south") : null;
        Available = probe != null && probe.Length > 0;

        if (target == null) return;

        if (Available)
        {
            target.color = c.tint;

            // PlayerProgress lerps its damage flash from a colour it captured once, and writes
            // that colour back every frame. Without this the tint set here survives exactly one
            // frame before the first character's colour is pinned back over it.
            if (PlayerProgress.Instance != null) PlayerProgress.Instance.RecaptureBodyColour();

            // Scaled on the Body child, which is below the transform PlayerController.SetScale
            // drives for growth, so the two multiply instead of fighting.
            target.transform.localScale = Vector3.one * (CanvasScale * Mathf.Max(0.01f, c.displayScale));

            // Everything needed to explain an on-screen size, in one line: if a kaiju looks
            // wrong next to the others, this says whether the frames, the scale or the tier
            // is responsible without anyone having to measure a screenshot.
            var frame = probe[0];
            float parent = target.transform.parent != null ? target.transform.parent.localScale.x : 1f;
            Debug.Log($"KRZ art: {c.displayName} — frame {frame.rect.height}px @ {frame.pixelsPerUnit} ppu, " +
                      $"displayScale {c.displayScale}, body scale {target.transform.localScale.x:0.###}, " +
                      $"art parent scale {parent:0.###}");
        }
        else
        {
            // Greybox is authored at world scale and wants the run's own tint.
            if (greybox != null) target.sprite = greybox;
            target.color = tuning != null ? tuning.playerTint : Color.white;
            target.transform.localScale = Vector3.one;

            string name = c != null ? c.displayName : "(no roster)";
            Debug.LogWarning($"KRZ: no frames for {name}, staying on greybox.");
        }
    }

    /// <summary>Plays a clip once, falling back to idle or walk when it finishes.</summary>
    public void PlayOnce(Clip clip)
    {
        // A clip already playing is left alone rather than restarted. Being hit twice inside
        // a quarter of a second would otherwise reset the recoil to frame 0 each time and the
        // character would never move past its first pose.
        if (oneShot && current == clip && Time.time - clipStartedAt < FrameCount(clip) / Fps) return;

        current = clip;
        clipStartedAt = Time.time;
        oneShot = true;
    }

    int FrameCount(Clip clip)
    {
        var counts = character?.frameCounts;
        return counts != null && (int)clip < counts.Length ? Mathf.Max(1, counts[(int)clip]) : 1;
    }

    void LateUpdate()
    {
        // The select screen can change the pick while the world sits frozen behind it, so
        // the character is re-resolved rather than captured once in Awake.
        var wanted = Resolve();
        if (!applied || wanted != character)
        {
            Apply(wanted);
            applied = target != null;
        }

        if (!Available || target == null || player == null) return;

        var progress = PlayerProgress.Instance;
        int level = Mathf.Clamp((progress != null ? progress.Tier : 0) + 1, 1, 5);

        int facing = Mathf.Clamp(player.Facing, 0, 7);
        var frames = Load(character, level, current, DirFolder[facing]);

        if (frames == null || frames.Length == 0)
        {
            // Fall back rather than blank the character.
            frames = Load(character, level, Clip.Idle, DirFolder[facing]);
            if (frames == null || frames.Length == 0) return;
        }

        float elapsed = Time.time - clipStartedAt;
        int index = Mathf.FloorToInt(elapsed * FpsFor(level, current));

        if (oneShot && index >= frames.Length)
        {
            // Hand back to whichever loop is actually correct now, rather than always idle
            // and then switching to walk on the same frame — two resets in one frame showed
            // up as a stutter at the end of every swipe.
            oneShot = false;
            current = player.IsMoving ? Clip.Walk : Clip.Idle;
            clipStartedAt = Time.time;
            index = 0;

            frames = Load(character, level, current, DirFolder[facing]);
            if (frames == null || frames.Length == 0) return;
        }

        if (!oneShot)
        {
            // Driven by input intent, not velocity. Being jostled by a crowd produced
            // velocity the player never asked for, oscillating across the threshold and
            // resetting the loop to frame 0 every time it crossed.
            var loop = player.IsMoving ? Clip.Walk : Clip.Idle;
            if (loop != current)
            {
                current = loop;
                clipStartedAt = Time.time;
                index = 0;
                frames = Load(character, level, current, DirFolder[facing]);
                if (frames == null || frames.Length == 0) return;
            }
            index %= frames.Length;
        }

        target.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        target.flipX = DirFlip[facing];
    }

    /// <summary>Playback rate for a clip. The patched level 2 idle sets its own.</summary>
    float FpsFor(int level, Clip clip) => IsPatched(character, level, clip) ? Level2IdleFps : Fps;

    static bool IsPatched(CharacterType c, int level, Clip clip)
        => c != null && c.patchLevel2Idle && level == 2 && clip == Clip.Idle;

    /// <summary>
    /// Substitutes {level} into a character's folder or prefix. A template without the token
    /// is returned as-is, which is how one baked set serves all five tiers.
    /// </summary>
    static string WithLevel(string template, int level)
        => string.IsNullOrEmpty(template) ? string.Empty : template.Replace("{level}", level.ToString());

    /// <summary>
    /// Loads the raw textures and builds sprites in code, rather than loading Sprite assets
    /// that depend on Unity import settings being right.
    ///
    /// A PNG imported with default settings is still a Texture2D, so this works on a fresh
    /// clone with no menu item run and no per-machine setup — the same reason the rest of
    /// the project builds itself at Play instead of being wired in the Editor. Pivot and PPU
    /// come from the art spec, not from the .meta.
    /// </summary>
    static Sprite[] Load(CharacterType c, int level, Clip clip, string direction)
    {
        if (c == null) return null;

        string folder = WithLevel(c.artFolder, level);
        string key = $"{folder}/{ClipNames[(int)clip]}/{direction}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        // A patched clip borrows frames from a sibling that loaded cleanly, so the
        // substitution is invisible to everything above this method.
        if (IsPatched(c, level, clip))
        {
            var source = Load(c, level, Clip.Walk, direction);
            if (source == null || source.Length == 0) { cache[key] = null; return null; }

            var borrowed = new Sprite[Level2IdleFrames.Length];
            for (int i = 0; i < borrowed.Length; i++)
                borrowed[i] = source[Mathf.Clamp(Level2IdleFrames[i], 0, source.Length - 1)];

            cache[key] = borrowed;
            return borrowed;
        }

        var counts = c.frameCounts;
        int count = counts != null && (int)clip < counts.Length ? counts[(int)clip] : 0;
        if (count <= 0) { cache[key] = null; return null; }

        string prefix = WithLevel(c.artPrefix, level);
        var frames = new Sprite[count];

        for (int i = 0; i < count; i++)
        {
            var tex = Resources.Load<Texture2D>(
                $"{key}/{prefix}_{ClipNames[(int)clip]}_{direction}_{i:00}");
            if (tex == null) { cache[key] = null; return null; }

            frames[i] = Sprite.Create(tex,
                                      new Rect(0f, 0f, tex.width, tex.height),
                                      Pivot, PixelsPerUnit,
                                      0, SpriteMeshType.FullRect);
        }

        cache[key] = frames;
        return frames;
    }

    /// <summary>
    /// Frame 0 of a character's south-facing idle, for the select grid's portrait. Loaded as
    /// a raw texture rather than through Load so the grid can draw it with GUI.DrawTexture
    /// and does not pay for building five sprites it will not animate.
    /// </summary>
    public static Texture2D Portrait(CharacterType c)
    {
        if (c == null) return null;
        string folder = WithLevel(c.artFolder, 1);
        string prefix = WithLevel(c.artPrefix, 1);
        return Resources.Load<Texture2D>($"{folder}/idle/south/{prefix}_idle_south_00");
    }

    public static void ClearCache() => cache.Clear();
}
