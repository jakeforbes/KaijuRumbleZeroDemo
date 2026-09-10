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

    string current = "idle";
    float clipStartedAt;
    bool oneShot;
    int facing;

    void Awake()
    {
        Available = type != null && !string.IsNullOrEmpty(type.artFolder) &&
                    Frames("idle", 0) != null;
    }

    Sprite[] Frames(string clip, int face) =>
        DirectionalArt.Load(type.artFolder, type.artPrefix, clip, face,
                            FrameCount(clip), type.artLongDirectionNames,
                            type.artFrameDigits, type.artFirstFrame);

    int FrameCount(string clip) => clip switch
    {
        "walk" => type.walkFrames,
        "attack" => type.attackFrames,
        "hit" => type.hitFrames,
        "destruction" => type.deathFrames,
        _ => type.idleFrames,
    };

    public void Play(string clip, bool once)
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
                if (current == "destruction") index = frames.Length - 1;
                else
                {
                    oneShot = false;
                    current = owner.Velocity.sqrMagnitude > 0.05f ? "walk" : "idle";
                    clipStartedAt = Time.time;
                    index = 0;
                    frames = Frames(current, facing);
                    if (frames == null || frames.Length == 0) return;
                }
            }
        }
        else
        {
            var wanted = owner.Velocity.sqrMagnitude > 0.05f ? "walk" : "idle";
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
        target.flipX = DirectionalArt.Mirrored[facing];
    }

    static int FacingFrom(Vector2 v)
    {
        float deg = Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;
        if (deg < 0f) deg += 360f;
        return Mathf.RoundToInt(deg / 45f) % 8;
    }
}
