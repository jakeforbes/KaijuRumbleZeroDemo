using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Player movement. The world is screen space: X is horizontal, Y is depth up the
/// screen. Vertical input is scaled by Tuning.isoSquash so a circular stick input
/// traces the ellipse a 2:1 isometric projection expects, instead of reading flat.
/// </summary>
[RequireComponent(typeof(Rigidbody2D))]
[SoundActions(Sfx.Footstep, Sfx.KaijuImpact)]
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

    /// <summary>Extra degrees past a facing boundary before the sprite direction changes.</summary>
    const float FacingHysteresis = 8f;

    static readonly string[] FacingNames = { "s", "se", "e", "ne", "n", "nw", "w", "sw" };
    public string FacingName => FacingNames[Facing];

    /// <summary>True while the Abomination's roar owns the kaiju's movement.</summary>
    public bool BeingKnockedBack => Time.time < knockbackUntil;

    Rigidbody2D body;
    CapsuleCollider2D footprint;
    Transform art;
    Vector2 desired;

    Vector2 knockbackDir;
    float knockbackSpeed;
    float knockbackFrom;
    float knockbackUntil;

    void Awake()
    {
        body = GetComponent<Rigidbody2D>();
        footprint = GetComponent<CapsuleCollider2D>();
        body.gravityScale = 0f;
        body.freezeRotation = true;
        body.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        body.interpolation = RigidbodyInterpolation2D.Interpolate;
    }

    public void BindArt(Transform artRoot) => art = artRoot;

    /// <summary>
    /// A thrown kaiju hitting a building wrecks it. Only while the roar is carrying
    /// you — walking into a wall the rest of the time has to stay free, or every
    /// scrape along a street would level the block.
    ///
    /// Damage goes through the ordinary path, so the size-versus-class rule still
    /// applies. Being flung into something two classes above you dents it; being
    /// flung into a shack removes the shack.
    /// </summary>
    void OnCollisionEnter2D(Collision2D collision)
    {
        if (!BeingKnockedBack) return;

        var building = collision.collider.GetComponentInParent<Building>();
        if (building == null || !building.IsAlive) return;

        CancelKnockback();
        body.linearVelocity = Vector2.zero;

        Vector2 at = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
        building.TakeDamage(tuning.knockbackImpactDamage, transform.position);

        AudioEvents.Play(Sfx.KaijuImpact, at, owner: gameObject);
        HitFx.Burst(at, new Color(1f, 0.8f, 0.45f), Scale * 0.8f, tuning.pixelsPerUnit, 0.35f);
        if (PlayerProgress.Instance != null)
            PlayerProgress.Instance.ShakeExternal(tuning.tierUpShake * 1.5f);
    }

    void Update()
    {
        var state = PlayerProgress.Instance;
        bool locked = (state != null && state.IsDead) || BeingKnockedBack;
        Vector2 raw = locked ? Vector2.zero : ReadInput();

        // Squash the vertical component so movement matches the isometric projection.
        desired = new Vector2(raw.x, raw.y * tuning.isoSquash);
        if (desired.sqrMagnitude > 1f) desired.Normalize();

        if (raw.sqrMagnitude > 0.04f)
        {
            Facing = FacingFromInput(raw, Facing);
            AimDir = desired.normalized;
        }
    }

    /// <summary>
    /// Thrown clear of a point, decaying to a stop over the duration. Overrides input
    /// outright rather than adding an impulse: mass grows with the square of size, so
    /// by the time the Abomination arrives a force large enough to move a size-5 kaiju
    /// would launch everything else in the scene into orbit.
    ///
    /// Aimed on the flat ground plane and squashed back, so being hurled north-east
    /// travels the same ground distance as being hurled east.
    /// </summary>
    public void Knockback(Vector2 fromPoint, float distance, float seconds)
    {
        Vector2 away = (Vector2)transform.position - fromPoint;
        Vector2 flat = new Vector2(away.x, away.y / tuning.isoSquash);
        if (flat.sqrMagnitude < 0.0001f) flat = Random.insideUnitCircle;
        flat.Normalize();

        knockbackDir = new Vector2(flat.x, flat.y * tuning.isoSquash).normalized;
        seconds = Mathf.Max(0.05f, seconds);

        // Decaying linearly from v0 to nothing covers v0 * t / 2, so this is the
        // launch speed that lands exactly on the distance asked for.
        knockbackSpeed = 2f * distance / seconds;
        knockbackFrom = Time.time;
        knockbackUntil = Time.time + seconds;
        body.linearVelocity = knockbackDir * knockbackSpeed;
    }

    /// <summary>Called on impact, so the kaiju stops instead of grinding into a wall.</summary>
    public void CancelKnockback() => knockbackUntil = 0f;

    void FixedUpdate()
    {
        if (BeingKnockedBack)
        {
            float t = Mathf.InverseLerp(knockbackFrom, knockbackUntil, Time.time);
            body.linearVelocity = knockbackDir * (knockbackSpeed * (1f - t));
            return;
        }

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
        if (footprint != null && tuning != null) footprint.size = tuning.playerFootprintFraction * s;

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

    /// <summary>
    /// Snaps any input direction to one of eight facings, with hysteresis: the aim has
    /// to clear the boundary by a margin before the facing changes.
    ///
    /// Without it, an input sitting on a boundary — a diagonal on the keys, or a stick
    /// wavering — alternates between two adjacent renders every frame. Three of the
    /// eight facings are horizontal mirrors, so that alternation also flips the sprite,
    /// which reads as the character rocking violently in place.
    /// </summary>
    static int FacingFromInput(Vector2 v, int currentFacing)
    {
        float deg = Mathf.Atan2(v.x, -v.y) * Mathf.Rad2Deg;   // 0 = south, clockwise
        if (deg < 0f) deg += 360f;

        int candidate = Mathf.RoundToInt(deg / 45f) % 8;
        if (candidate == currentFacing) return currentFacing;

        float offCentre = Mathf.Abs(Mathf.DeltaAngle(deg, currentFacing * 45f));
        return offCentre > 22.5f + FacingHysteresis ? candidate : currentFacing;
    }
}
