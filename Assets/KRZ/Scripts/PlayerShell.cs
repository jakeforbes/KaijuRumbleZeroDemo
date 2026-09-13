using UnityEngine;

/// <summary>
/// The Shell upgrade: a shield that forms on its own timer and holds for a few seconds.
///
/// The only defensive pickup in the set, and the only one that gives back time rather than
/// damage. Stacks shorten the wait instead of strengthening the shield — it either holds or
/// it does not — so levelling it is read on the clock, as the gaps between windows closing.
///
/// A component on the player rather than a timer inside PlayerSpecial, following ToxicField:
/// it owns a visual that has to live and fade with it, and PlayerProgress has to be able to
/// ask whether it is up without reaching through the ability that fires the Blast.
/// </summary>
public class PlayerShell : MonoBehaviour
{
    /// <summary>
    /// Whether a shield is up right now. Static because PlayerProgress.Invulnerable is the
    /// one gate every source of damage already passes through, and it is reached from places
    /// that have no handle on the player's components.
    /// </summary>
    public static bool Active { get; private set; }

    /// <summary>Static state needs clearing by hand or a restart begins mid-shield.</summary>
    public static void Reset() => Active = false;

    public Tuning tuning;

    static Sprite sprite;

    SpriteRenderer art;
    PlayerUpgrades upgrades;
    PlayerProgress progress;

    float nextShellAt;
    float shellUntil;

    void Awake()
    {
        upgrades = GetComponent<PlayerUpgrades>();
        progress = GetComponent<PlayerProgress>();
    }

    void OnDestroy()
    {
        if (Active) Active = false;
    }

    void Update()
    {
        if (progress == null || upgrades == null) return;

        // No shield while the run is over. A bubble still pulsing over a corpse reads as the
        // game not having noticed.
        if (progress.RunOver) { Dismiss(); return; }

        if (!upgrades.HasShell)
        {
            // The upgrade can only be gained, never lost, but a restart reuses these objects
            // and the timer has to start from the first stack rather than from scene load.
            nextShellAt = 0f;
            Dismiss();
            return;
        }

        // Armed on the frame the first stack lands rather than at scene load, so picking Shell
        // up does not hand you a shield that was already most of the way through its wait.
        if (nextShellAt <= 0f) nextShellAt = Time.time + upgrades.ShellInterval;

        if (Active && Time.time >= shellUntil) Dismiss();

        if (!Active && Time.time >= nextShellAt) Raise();

        if (Active) Draw();
    }

    void Raise()
    {
        Active = true;
        shellUntil = Time.time + tuning.shellDuration;

        // The next window is measured from this one opening, not from it closing, so the
        // interval the upgrade advertises is the period a player can actually count on.
        nextShellAt = Time.time + Mathf.Max(tuning.shellDuration + 0.1f, upgrades.ShellInterval);

        EnsureArt();
        if (art != null) art.enabled = true;
        ShockwaveFx.Show(transform.position, ShellColour, 0.6f, 0.5f, 0.3f);
    }

    void Dismiss()
    {
        if (!Active && (art == null || !art.enabled)) return;
        Active = false;
        if (art != null) art.enabled = false;
    }

    static Color ShellColour => new(0.55f, 0.8f, 1f);

    void EnsureArt()
    {
        if (art != null) return;
        if (sprite == null) sprite = GreyboxArt.Bubble(160, tuning.pixelsPerUnit);

        var go = new GameObject("Shell");
        go.transform.SetParent(transform, false);

        art = go.AddComponent<SpriteRenderer>();
        art.sprite = sprite;

        // Drawn in front of the kaiju it wraps. Sorting by pivot like everything else would
        // put a bubble centred on the body behind the body's own feet.
        art.sortingOrder = 40;
    }

    /// <summary>
    /// Sized to the kaiju every frame rather than at launch, so a shield up through a tier-up
    /// grows with it instead of ending up worn like a belt.
    /// </summary>
    void Draw()
    {
        if (art == null) return;

        float body = progress.Scale;
        art.transform.localPosition = Vector3.up * (body * 0.5f);
        art.transform.localScale = Vector3.one * (body * 1.5f * tuning.pixelsPerUnit / 160f);

        // Fades out over its last half second and pulses gently before that, so the moment it
        // stops protecting you is visible ahead of time rather than announced by taking a hit.
        float left = shellUntil - Time.time;
        float fade = Mathf.Clamp01(left / 0.5f);
        float pulse = 0.78f + 0.22f * Mathf.Sin(Time.time * 6f);

        var c = ShellColour;
        art.color = new Color(c.r, c.g, c.b, 0.85f * fade * pulse);
    }
}
