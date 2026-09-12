using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Both of the game's menus: the title the game opens on, and the Continue / Restart
/// overlay Esc or the pad's Menu button raises mid-run. They are the same screen with
/// a different heading and a different list, so they share one implementation — the
/// world is frozen behind either one, and everything about navigating them matches.
///
/// Built in IMGUI like the rest of the HUD rather than as a uGUI canvas, so it needs
/// no EventSystem and no prefab — the project has neither, and adding them for three
/// buttons would mean the only hand-wired objects in the game. IMGUI has no focus
/// navigation of its own, so the highlight is tracked here and drawn by hand.
/// </summary>
public sealed class PauseMenu : MonoBehaviour
{
    /// <summary>True while the menu owns the screen.</summary>
    public static bool IsPaused { get; private set; }

    /// <summary>
    /// Whether gameplay should ignore input this frame.
    ///
    /// Confirm shares its bindings with Blast — Space and pad A are both — so a press
    /// that closes the menu would otherwise also fire a Blast, because PlayerSpecial
    /// reads wasPressedThisFrame independently of anything here. Staying true for the
    /// remainder of the frame that resumed swallows that press regardless of which
    /// component's Update ran first, which is undefined between the two.
    /// </summary>
    public static bool BlockingInput => IsPaused || Time.frameCount == resumedFrame;

    static int resumedFrame = -1;

    /// <summary>
    /// True while the title is up. The run's meters stay off it — a full health bar and
    /// an empty food meter under the title advertise a run that has not started.
    /// </summary>
    public static bool AtTitle { get; private set; }

    /// <summary>How loud the music sits while paused, relative to its normal level.</summary>
    public static float MusicDuck { get; private set; } = 1f;

    static float musicDuckTarget = 1f;

    /// <summary>Seconds the duck takes to reach its target, in each direction.</summary>
    const float DuckFadeSeconds = 0.18f;

    Tuning tuning;
    int highlighted;
    float timeScaleBeforePause = 1f;

    /// <summary>Set while a stick or key is held, so one deflection moves one option.</summary>
    bool navLatched;

    /// <summary>Hover only steals the highlight once the mouse has actually moved.</summary>
    Vector2 lastMousePosition;
    bool mouseMoved;

    /// <summary>Which menu is up. They differ only in heading and list.</summary>
    enum Mode { Title, Pause }

    Mode mode;

    static readonly string[] TitleOptions = { "New Game" };
    static readonly string[] PauseOptions = { "Continue", "Restart" };

    string[] options = PauseOptions;
    Rect[] optionRects = new Rect[2];

    /// <summary>
    /// Set by Restart so the rebuilt scene goes straight into a run instead of stopping
    /// at the title. Deliberately outside Reset(): Restart sets it before the scene
    /// reloads and Reset runs after, so clearing it there would undo the request.
    /// </summary>
    static bool skipTitle;

    public static void SkipTitleOnce() => skipTitle = true;

    GUIStyle labelStyle;
    GUIStyle titleStyle;

    /// <summary>Static state has to be cleared by hand, or a restart inherits the last run's pause.</summary>
    public static void Reset()
    {
        IsPaused = false;
        AtTitle = false;
        resumedFrame = -1;
        MusicDuck = musicDuckTarget = 1f;
        AudioListener.pause = false;
    }

    void Awake()
    {
        tuning = Resources.Load<Tuning>("Tuning");

        // The gym is a review scene with nothing to start, and a restart has already
        // asked for a run, so neither stops at the title.
        bool wantsTitle = !skipTitle && !GameBootstrap.IsGym;
        skipTitle = false;
        if (wantsTitle) Open(Mode.Title);
    }

    void OnDestroy()
    {
        // The scene reloads out from under a paused menu on Restart. Without this the
        // listener would stay muted and the next run would start frozen.
        if (IsPaused) Reset();
    }

    void Update()
    {
        // Unscaled, because the whole point is that it runs while the world is frozen.
        // Stepping the volume instead of ramping it reads as a glitch rather than as
        // the music stepping back.
        MusicDuck = Mathf.MoveTowards(MusicDuck, musicDuckTarget,
                                      Time.unscaledDeltaTime / DuckFadeSeconds);

        var kb = Keyboard.current;
        var pad = Gamepad.current;

        if (Mouse.current != null)
        {
            var p = Mouse.current.position.ReadValue();
            if ((p - lastMousePosition).sqrMagnitude > 1f) mouseMoved = true;
            lastMousePosition = p;
        }

        bool toggle = (kb != null && kb.escapeKey.wasPressedThisFrame) ||
                      (pad != null && pad.startButton.wasPressedThisFrame);

        if (!IsPaused)
        {
            if (toggle && CanPause()) Open(Mode.Pause);
            return;
        }

        // Esc backs out of the pause menu. There is nothing behind the title to back
        // out to, so it does nothing there rather than dropping into a frozen run.
        if (toggle && mode == Mode.Pause) { Resume(); return; }

        Navigate(kb, pad);

        bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame ||
                                       kb.numpadEnterKey.wasPressedThisFrame ||
                                       kb.spaceKey.wasPressedThisFrame)) ||
                       (pad != null && pad.buttonSouth.wasPressedThisFrame);
        if (confirm) Choose(highlighted);
    }

    /// <summary>
    /// Win and death already own the screen with their own Restart, and the boss
    /// introduction runs its camera move on unscaled time — pausing through it would
    /// freeze the world while the cinematic carried on playing over the top.
    /// </summary>
    static bool CanPause()
    {
        if (CameraRig.IsBossIntroductionPlaying) return false;
        var progress = PlayerProgress.Instance;
        return progress == null || !progress.RunOver;
    }

    void Open(Mode m)
    {
        mode = m;
        options = m == Mode.Title ? TitleOptions : PauseOptions;
        if (optionRects.Length != options.Length) optionRects = new Rect[options.Length];

        IsPaused = true;
        AtTitle = m == Mode.Title;
        highlighted = 0;              // The first option, every time a menu opens.
        mouseMoved = false;

        // Latched, not clear: pausing mid-stride means a movement key is probably held,
        // and an unlatched axis would read that as a deliberate press and move the
        // highlight off the default on the first frame the menu was up.
        navLatched = true;
        timeScaleBeforePause = Time.timeScale;
        Time.timeScale = 0f;

        // Silences effects and voices. The music sources opt out of listener pause, so
        // they keep playing: ducked under the pause menu, which reads as the game
        // waiting, and at full level under the title, which is the front door rather
        // than an interruption of anything.
        AudioListener.pause = true;
        musicDuckTarget = m == Mode.Title ? 1f
                        : tuning != null ? tuning.pauseMusicVolume
                        : 0.35f;
    }

    void Resume()
    {
        IsPaused = false;
        AtTitle = false;
        resumedFrame = Time.frameCount;

        // Restores whatever the speed cheats had set rather than assuming 1, so pausing
        // mid-test does not quietly undo a [ or ] press.
        Time.timeScale = timeScaleBeforePause <= 0f ? 1f : timeScaleBeforePause;
        AudioListener.pause = false;
        musicDuckTarget = 1f;
    }

    void Choose(int option)
    {
        // Resume either way: Restart reloads the scene, and an unpause that never ran
        // would leave the listener muted for the new run.
        Resume();

        // New Game just starts the run: the scene is already built and waiting behind
        // the title, so there is nothing to reload.
        if (mode == Mode.Pause && option == 1) GameBootstrap.Restart();
    }

    /// <summary>
    /// One option per deflection. The stick has to return near centre before it moves
    /// again, or a held direction would run the highlight up and down the list at frame
    /// rate — and unlike a key repeat there is nothing to feel that against.
    /// </summary>
    void Navigate(Keyboard kb, Gamepad pad)
    {
        float v = 0f;
        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed) v -= 1f;
        }
        if (pad != null)
        {
            float stick = pad.leftStick.ReadValue().y;
            if (Mathf.Abs(stick) > 0.5f) v += stick;
            v += pad.dpad.ReadValue().y;
        }

        if (Mathf.Abs(v) < 0.5f) { navLatched = false; return; }
        if (navLatched) return;

        navLatched = true;
        highlighted = (highlighted + (v > 0f ? options.Length - 1 : 1)) % options.Length;

        // The pad or keys taking over hands the highlight back to them until the mouse
        // is moved again, so a cursor left sitting over Restart cannot hold it.
        mouseMoved = false;
    }

    void OnGUI()
    {
        if (!IsPaused) return;

        // Lower depth draws on top. The debug HUD, the food meter and the boss arrow
        // all have their own OnGUI, and the arrow animates on unscaled time, so without
        // this it would keep pulsing over the menu depending on component order.
        GUI.depth = -100;

        labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        titleStyle ??= new GUIStyle(labelStyle) { fontSize = 30 };

        // Dims the whole run behind the box, so the frozen frame reads as background.
        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        // The title needs the width for its name and one row fewer than the pause menu.
        bool title = mode == Mode.Title;
        float w = title ? 460f : 340f;
        float h = title ? 160f : 200f;

        var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.92f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(box.x, box.y + 18f, box.width, 40f),
                  title ? "<b>KAIJU RUMBLE ZERO</b>" : "<b>PAUSED</b>", titleStyle);

        const float bw = 220f, bh = 40f;
        for (int i = 0; i < options.Length; i++)
            optionRects[i] = new Rect(box.center.x - bw * 0.5f, box.y + 76f + i * (bh + 12f), bw, bh);

        if (mouseMoved)
            for (int i = 0; i < optionRects.Length; i++)
                if (optionRects[i].Contains(Event.current.mousePosition)) highlighted = i;

        for (int i = 0; i < options.Length; i++)
        {
            bool on = i == highlighted;
            GUI.color = on ? new Color(1f, 0.66f, 0.24f, 0.95f) : new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(optionRects[i], Texture2D.whiteTexture);

            GUI.color = on ? new Color(0.08f, 0.06f, 0.03f) : Color.white;
            GUI.Label(optionRects[i], on ? $"<b>{options[i]}</b>" : options[i], labelStyle);
            GUI.color = Color.white;

            // Invisible button over the drawn one: the click handling is GUI.Button's,
            // the look is ours, and the two never disagree about where the edge is.
            if (GUI.Button(optionRects[i], GUIContent.none, GUIStyle.none)) { Choose(i); return; }
        }

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(box.x, box.y + h - 30f, box.width, 20f),
                  title ? "Enter, Space or A to begin" : "Esc / Menu to close", labelStyle);
        GUI.color = Color.white;
    }
}
