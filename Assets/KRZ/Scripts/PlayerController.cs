using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player movement. The world is screen space: X is horizontal, Y is depth up the
/// screen. Vertical input is scaled by Tuning.isoSquash so a circular stick input
/// traces the ellipse a 2:1 isometric projection expects, instead of reading flat.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[SoundActions(Sfx.Footstep)]
public class PlayerController : MonoBehaviour
{
    public Tuning tuning;

    /// <summary>Current facing, 0..7 starting at south and going clockwise. Drives sprite choice later.</summary>
    public int Facing { get; private set; }

    /// <summary>Size multiplier. Growth takes this over in Stage 3.</summary>
    public float Scale { get; private set; } = 1f;

    /// <summary>Last direction moved, on the squashed ground plane. Attacks aim along it.</summary>
    public Vector2 AimDir { get; private set; } = Vector2.down;

    public Vector2 Velocity => body.linearVelocity;

    /// <summary>
    /// Whether the player is asking to move, as opposed to being shoved. Animation
    /// state reads this rather than rigidbody velocity: a crowd of enemies pushing
    /// against you produces velocity you did not ask for, which flickered the walk
    /// and idle loops against each other and restarted them every frame.
    /// </summary>
    public bool IsMoving => desired.sqrMagnitude > 0.0025f;

    /// <summary>Stick movement below this is treated as drift, not intent.</summary>
    const float DeadZone = 0.2f;

    static readonly string[] FacingNames = { "s", "se", "e", "ne", "n", "nw", "w", "sw" };
    public string FacingName => FacingNames[Facing];

    Rigidbody2D body;
    CapsuleCollider2D footprint;
    Vector2 baseFootprint;
    Transform art;
    Vector2 desired;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        footprint = GetComponent<CapsuleCollider2D>();
        if (footprint != null) baseFootprint = footprint.size;
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void BindArt(Transform artRoot) => art = artRoot;

    void Update()
    {
        var state = PlayerProgress.Instance;
        Vector2 raw = state != null && state.IsDead ? Vector2.zero : ReadInput();

        // Squash the vertical component so movement matches the isometric projection.
        desired = new Vector2(raw.x, raw.y * tuning.isoSquash);
        if (desired.sqrMagnitude > 1f) desired.Normalize();

        if (raw.sqrMagnitude > 0.04f)
        {
            Facing = FacingFromInput(raw);
            AimDir = desired.normalized;
        }
    }

    void FixedUpdate()
    {
        // Speed compounds per tier, not with raw scale: a 4x kaiju moving 4x as fast
        // would outrun the camera and the arena. Growth should feel like an upgrade,
        // not a different game.
        var progress = PlayerProgress.Instance;
        var upgrades = PlayerUpgrades.Instance;
        float speed = tuning.moveSpeed
                      * (progress != null ? progress.SpeedMultiplier : 1f)
                      * (upgrades != null ? upgrades.MoveSpeedMul : 1f);
        Vector2 target = desired * speed;
        float rate = desired.sqrMagnitude > 0.001f ? tuning.acceleration : tuning.deceleration;
        body.linearVelocity = Vector2.MoveTowards(body.linearVelocity, target, rate * Time.fixedDeltaTime);
    }

    public void SetScale(float s)
    {
        Scale = s;
        if (art != null) art.localScale = Vector3.one * s;
        if (footprint != null) footprint.size = baseFootprint * s;

        // Mass grows with the square of size, so collisions with infantry move them
        // and not you. A giant monster being jostled by soldiers reads as wrong, and
        // the disparity should widen as you grow. The Abomination's knockback is a
        // deliberate ability rather than a physics outcome.
        if (body != null && tuning != null) body.mass = tuning.playerMass * s * s;
    }

    static Vector2 ReadInput()
    {
        Vector2 keys = Vector2.zero;
        var kb = Keyboard.current;
        if (kb != null)
        {
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed) keys.x -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed) keys.x += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) keys.y -= 1f;
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) keys.y += 1f;
            if (keys.sqrMagnitude > 1f) keys.Normalize();
        }

        Vector2 pad = Vector2.zero;
        var gamepad = Gamepad.current;
        if (gamepad != null)
        {
            pad = gamepad.leftStick.ReadValue();
            if (pad.sqrMagnitude < DeadZone * DeadZone) pad = Vector2.zero;
        }

        // Whichever is being pushed harder wins. Reading the pad first and only
        // falling back to keys meant a controller with any stick drift silently
        // locked out the keyboard and walked the kaiju on its own.
        return pad.sqrMagnitude > keys.sqrMagnitude ? pad : keys;
    }

    /// <summary>Snaps any input direction to one of eight facings for sprite selection.</summary>
    static int FacingFromInput(Vector2 v)
    {
        float deg = Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;   // 0 = south, clockwise
        if (deg < 0f) deg += 360f;
        return Mathf.RoundToInt(deg / 45f) % 8;
    }
}
