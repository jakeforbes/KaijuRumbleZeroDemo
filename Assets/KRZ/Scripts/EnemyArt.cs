using UnityEngine;

/// <summary>
/// Plays an enemy's delivered sprites. Falls back to the greybox capsule when the
/// type has no art folder, so the roster can be half-illustrated without breaking.
///
/// Enemies drive facing from their own movement rather than from input, and the
/// destruction clip is held on the last frame while the corpse fades, so a kill
/// reads as an event rather than an object blinking out.
/// </summary>
public class EnemyArt : MonoBehaviour
{
    public Enemy owner;
    public SpriteRenderer target;
    public EnemyType type;

    public bool Available { get; private set; }

    int current = Idle;
    float clipStartedAt;
    bool oneShot;
    int facing;

    /// <summary>
    /// Called after the fields are assigned, never from Awake. AddComponent runs Awake
    /// immediately, before the caller can set anything, so probing for art there tested
    /// a null type and always reported none.
    /// </summary>
    public bool Init(Enemy enemy, SpriteRenderer renderer, EnemyType enemyType)
    {
        owner = enemy;
        target = renderer;
        type = enemyType;

        Available = type != null && !string.IsNullOrEmpty(type.artFolder) &&
                    Frames(Idle, 0) != null;
        return Available;
    }

    /// <summary>Clip slots, in the order artClipNames lists them.</summary>
    public const int Idle = 0, Walk = 1, Attack = 2, Hit = 3, Death = 4;

    ArtConfig Config => new()
    {
        folder = type.artFolder,
        prefix = type.artPrefix,
        pathFormat = type.artPathFormat,
        frameDigits = type.artFrameDigits,
        firstFrame = type.artFirstFrame,
        style = type.artDirectionStyle,
        mirrored = type.artMirrored,
    };

    string ClipName(int slot) =>
        type.artClipNames != null && slot < type.artClipNames.Length
            ? type.artClipNames[slot]
            : "idle";

    Sprite[] Frames(int slot, int face) =>
        DirectionalArt.Load(Config, ClipName(slot), face, FrameCount(slot));

    int FrameCount(int slot) => slot switch
    {
        Walk => type.walkFrames,
        Attack => type.attackFrames,
        Hit => type.hitFrames,
        Death => type.deathFrames,
        _ => type.idleFrames,
    };

    public void Play(int clip, bool once)
    {
        if (!Available) return;
        current = clip;
        clipStartedAt = Time.time;
        oneShot = once;
    }

    void LateUpdate()
    {
        if (!Available || target == null || owner == null) return;

        // Facing follows actual motion. Enemies have no input to read, and while
        // holding station their velocity is zero, so the last heading is kept.
        var v = owner.Velocity;
        if (v.sqrMagnitude > 0.05f) facing = FacingFrom(v);

        var frames = Frames(current, facing);
        if (frames == null || frames.Length == 0) return;

        int index = Mathf.FloorToInt((Time.time - clipStartedAt) * DirectionalArt.Fps);

        if (oneShot)
        {
            // Death holds its final pose; other one-shots hand back to a loop.
            if (index >= frames.Length)
            {
                if (current == Death) index = frames.Length - 1;
                else
                {
                    oneShot = false;
                    current = owner.Velocity.sqrMagnitude > 0.05f ? Walk : Idle;
                    clipStartedAt = Time.time;
                    index = 0;
                    frames = Frames(current, facing);
                    if (frames == null || frames.Length == 0) return;
                }
            }
        }
        else
        {
            var wanted = owner.Velocity.sqrMagnitude > 0.05f ? Walk : Idle;
            if (wanted != current)
            {
                current = wanted;
                clipStartedAt = Time.time;
                index = 0;
                frames = Frames(current, facing);
                if (frames == null || frames.Length == 0) return;
            }
            index %= frames.Length;
        }

        target.sprite = frames[Mathf.Clamp(index, 0, frames.Length - 1)];
        target.flipX = DirectionalArt.IsFlipped(Config, facing);
    }

    static int FacingFrom(Vector2 v)
    {
        float deg = Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;
        if (deg < 0f) deg += 360f;
        return Mathf.RoundToInt(deg / 45f) % 8;
    }
}
