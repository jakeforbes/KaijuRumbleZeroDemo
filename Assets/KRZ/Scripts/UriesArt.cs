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

    // Facing index 0..7 is s, se, e, ne, n, nw, w, sw. The last three are mirrors.
    static readonly string[] DirFolder =
        { "south", "southeast", "east", "northeast", "north", "northeast", "east", "southeast" };
    static readonly bool[] DirFlip =
        { false, false, false, false, false, true, true, true };

    public const float Fps = 12f;

    /// <summary>Sprites are 512 px tall; size 1 shows at 128 px, so the base is a quarter.</summary>
    public const float CanvasScale = 0.25f;

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
        int index = Mathf.FloorToInt(elapsed * Fps);

        if (oneShot && index >= frames.Length)
        {
            oneShot = false;
            current = Clip.Idle;
            clipStartedAt = Time.time;
            index = 0;
        }

        if (!oneShot)
        {
            // Idle and walk loop, chosen by whether the kaiju is actually moving.
            var wanted = player.Velocity.sqrMagnitude > 0.4f ? Clip.Walk : Clip.Idle;
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

    static Sprite[] Load(int level, Clip clip, string direction)
    {
        string key = $"Uries/Level_{level}/{ClipNames[(int)clip]}/{direction}";
        if (cache.TryGetValue(key, out var cached)) return cached;

        int count = FrameCounts[clip];
        var frames = new Sprite[count];
        for (int i = 0; i < count; i++)
        {
            string file = $"uries_l{level}_{ClipNames[(int)clip]}_{direction}_{i:00}";
            frames[i] = Resources.Load<Sprite>($"{key}/{file}");
            if (frames[i] == null) { cache[key] = null; return null; }
        }

        cache[key] = frames;
        return frames;
    }

    public static void ClearCache() => cache.Clear();
}
