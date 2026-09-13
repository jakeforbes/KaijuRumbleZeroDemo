using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Every screen that freezes the game: the title it opens on, the character select behind
/// New Game, and the Continue / Restart overlay Esc or the pad's Menu button raises mid-run.
/// They share one implementation because they share everything that matters — the world is
/// frozen behind each, the same buttons confirm, and the same press has to be swallowed on
/// the way out. Only the contents differ, and only the grid navigates in two dimensions.
///
/// Built in IMGUI like the rest of the HUD rather than as a uGUI canvas, so it needs no
/// EventSystem and no prefab — the project has neither, and adding them here would mean the
/// only hand-wired objects in the game. IMGUI has no focus navigation of its own, so the
/// highlight is tracked here and drawn by hand.
/// </summary>
public sealed class PauseMenu : MonoBehaviour
{
    /// <summary>True while a menu owns the screen.</summary>
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
    /// True while a pre-run screen is up — the title or the character select. The run's
    /// meters stay off both: a full health bar and an empty food meter under the title
    /// advertise a run that has not started.
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

    /// <summary>Set while a stick or key is held, so one deflection moves one step.</summary>
    bool navLatched;

    /// <summary>Hover only steals the highlight once the mouse has actually moved.</summary>
    Vector2 lastMousePosition;
    bool mouseMoved;

    /// <summary>Which screen is up.</summary>
    enum Mode { Title, Select, Pause }

    Mode mode;

    static readonly string[] TitleOptions = { "New Game" };
    static readonly string[] PauseOptions = { "Continue", "Restart" };

    string[] options = PauseOptions;
    Rect[] optionRects = new Rect[2];

    // ---- character select ---------------------------------------------------

    /// <summary>
    /// The grid is a fixed 4x5 whatever the roster holds. Slots past the end are drawn
    /// locked rather than hidden, so the screen shows what the run could grow into instead
    /// of three portraits floating in a empty box.
    /// </summary>
    const int Cols = 4, Rows = 5, Cells = Cols * Rows;

    Rect[] cellRects = new Rect[Cells];
    Texture2D[] portraits;
    bool[] portraitLoaded;

    /// <summary>
    /// Set by Restart so the rebuilt scene opens on the character select rather than on the
    /// title. A restart is a request for another run, not a trip back to the front door, but
    /// it is also the natural moment to play someone else — so it stops one screen in.
    ///
    /// Deliberately outside Reset(): Restart sets it before the scene reloads and Reset runs
    /// after, so clearing it there would undo the request.
    /// </summary>
    static bool openSelectOnLoad;

    public static void OpenSelectOnReload() => openSelectOnLoad = true;

    GUIStyle labelStyle;
    GUIStyle titleStyle;
    GUIStyle smallStyle;
    GUIStyle lockStyle;

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

        // A restart drops straight onto the grid; a fresh play session starts at the title.
        // Either way the flag is cleared here, so it cannot leak into the next reload.
        bool toSelect = openSelectOnLoad;
        openSelectOnLoad = false;

        // The gym is a review scene with nothing to start and nobody to choose.
        if (!GameBootstrap.IsGym) Open(toSelect ? Mode.Select : Mode.Title);
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

        bool back = toggle || (pad != null && pad.buttonEast.wasPressedThisFrame);

        // Esc backs out of the pause menu, and out of the grid to the title. There is
        // nothing behind the title to back out to, so it does nothing there rather than
        // dropping into a frozen run.
        if (back && mode == Mode.Pause) { Resume(); return; }
        if (back && mode == Mode.Select) { Open(Mode.Title); return; }

        Navigate(kb, pad);

        // The right trigger is a button control here, so a pull reads as a press without
        // any threshold of our own.
        bool confirm = (kb != null && (kb.enterKey.wasPressedThisFrame ||
                                       kb.numpadEnterKey.wasPressedThisFrame ||
                                       kb.spaceKey.wasPressedThisFrame)) ||
                       (pad != null && (pad.buttonSouth.wasPressedThisFrame ||
                                        pad.rightTrigger.wasPressedThisFrame));
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
        options = m == Mode.Pause ? PauseOptions : TitleOptions;
        if (optionRects.Length != options.Length) optionRects = new Rect[options.Length];

        // Only capture the speed on the way in from gameplay. Title hands off to Select
        // while already frozen, and capturing there would record a timeScale of 0 as the
        // speed to restore.
        if (!IsPaused) timeScaleBeforePause = Time.timeScale;

        IsPaused = true;
        AtTitle = m != Mode.Pause;

        // The grid opens on whatever is already selected, so backing out and in again does
        // not silently move the pick. Every list opens on its first option.
        highlighted = m == Mode.Select ? Mathf.Clamp(CharacterArt.SelectedIndex, 0, Cells - 1) : 0;
        mouseMoved = false;

        // Latched, not clear: pausing mid-stride means a movement key is probably held,
        // and an unlatched axis would read that as a deliberate press and move the
        // highlight off the default on the first frame the menu was up.
        navLatched = true;
        Time.timeScale = 0f;

        // Silences effects and voices. The music sources opt out of listener pause, so
        // they keep playing: ducked under the pause menu, which reads as the game
        // waiting, and at full level under the front-door screens.
        AudioListener.pause = true;
        musicDuckTarget = m != Mode.Pause ? 1f
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
        if (mode == Mode.Select) { ChooseCharacter(option); return; }

        // New Game goes to the grid rather than into the run, and stays frozen on the way.
        if (mode == Mode.Title) { Open(Mode.Select); return; }

        // Resume either way: Restart reloads the scene, and an unpause that never ran
        // would leave the listener muted for the new run.
        Resume();
        if (option == 1) GameBootstrap.Restart();
    }

    /// <summary>
    /// Commits a grid cell. Locked and empty slots are inert rather than refused out loud:
    /// the question mark already says what they are, and a buzz on every stray press on a
    /// screen this sparse would be the loudest thing in the game.
    /// </summary>
    void ChooseCharacter(int cell)
    {
        var c = CharacterAt(cell);
        if (c == null || !c.unlocked) return;

        // The world behind the grid was already built with the previous pick, but nothing
        // needs rebuilding: CharacterArt re-resolves the roster entry every frame and swaps
        // the frames it loads, so setting the index is the whole commit.
        CharacterArt.SelectedIndex = cell;
        Resume();
    }

    CharacterType CharacterAt(int cell)
    {
        var roster = CharacterType.Roster;
        if (roster == null || cell < 0 || cell >= roster.Length) return null;
        return roster[cell];
    }

    /// <summary>
    /// One step per deflection. The stick has to return near centre before it moves again,
    /// or a held direction would run the highlight across the grid at frame rate — and
    /// unlike a key repeat there is nothing to feel that against.
    ///
    /// The grid reads both axes and the lists only the vertical, but the latch is shared:
    /// a diagonal push should move one cell, not two.
    /// </summary>
    void Navigate(Keyboard kb, Gamepad pad)
    {
        float h = 0f, v = 0f;

        if (kb != null)
        {
            if (kb.wKey.isPressed || kb.upArrowKey.isPressed || kb.numpad8Key.isPressed) v += 1f;
            if (kb.sKey.isPressed || kb.downArrowKey.isPressed || kb.numpad2Key.isPressed) v -= 1f;
            if (kb.aKey.isPressed || kb.leftArrowKey.isPressed || kb.numpad4Key.isPressed) h -= 1f;
            if (kb.dKey.isPressed || kb.rightArrowKey.isPressed || kb.numpad6Key.isPressed) h += 1f;
        }
        if (pad != null)
        {
            var stick = pad.leftStick.ReadValue();
            if (Mathf.Abs(stick.y) > 0.5f) v += stick.y;
            if (Mathf.Abs(stick.x) > 0.5f) h += stick.x;
            var dpad = pad.dpad.ReadValue();
            v += dpad.y;
            h += dpad.x;
        }

        if (mode != Mode.Select) h = 0f;

        if (Mathf.Abs(h) < 0.5f && Mathf.Abs(v) < 0.5f) { navLatched = false; return; }
        if (navLatched) return;
        navLatched = true;

        if (mode == Mode.Select)
        {
            // Dominant axis only, so a sloppy diagonal picks one direction instead of
            // jumping a row and a column at once.
            int row = highlighted / Cols, col = highlighted % Cols;
            if (Mathf.Abs(h) >= Mathf.Abs(v))
                col = (col + (h > 0f ? 1 : Cols - 1)) % Cols;
            else
                row = (row + (v > 0f ? Rows - 1 : 1)) % Rows;

            highlighted = row * Cols + col;
        }
        else
        {
            highlighted = (highlighted + (v > 0f ? options.Length - 1 : 1)) % options.Length;
        }

        // The pad or keys taking over hands the highlight back to them until the mouse is
        // moved again, so a cursor left sitting over a cell cannot hold it.
        mouseMoved = false;
    }

    void OnGUI()
    {
        if (!IsPaused) return;

        // Lower depth draws on top. The debug HUD, the food meter and the boss arrow all
        // have their own OnGUI, and the arrow animates on unscaled time, so without this it
        // would keep pulsing over the menu depending on component order.
        GUI.depth = -100;

        labelStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };
        titleStyle ??= new GUIStyle(labelStyle) { fontSize = 30 };
        smallStyle ??= new GUIStyle(labelStyle) { fontSize = 12 };
        lockStyle ??= new GUIStyle(labelStyle) { fontSize = 26 };

        // Dims the whole run behind the box, so the frozen frame reads as background.
        GUI.color = new Color(0f, 0f, 0f, mode == Mode.Select ? 0.72f : 0.55f);
        GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);

        if (mode == Mode.Select) DrawSelect();
        else DrawList();

        GUI.color = Color.white;
    }

    void DrawList()
    {
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

            // Invisible button over the drawn one: the click handling is GUI.Button's, the
            // look is ours, and the two never disagree about where the edge is.
            if (GUI.Button(optionRects[i], GUIContent.none, GUIStyle.none)) { Choose(i); return; }
        }

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(box.x, box.y + h - 30f, box.width, 20f),
                  title ? "Enter, Space or A to begin" : "Esc / Menu to close", labelStyle);
    }

    void DrawSelect()
    {
        // Sized off the screen rather than fixed, so the grid still fits at the small
        // window the editor opens in. The clamp keeps portraits from ballooning on a
        // large display, where a 200px cell would read as a menu of billboards.
        const float gap = 10f;
        float cell = Mathf.Clamp(
            Mathf.Min((Screen.width - 140f) / Cols, (Screen.height - 250f) / Rows) - gap,
            44f, 108f);

        float gridW = Cols * cell + (Cols - 1) * gap;
        float gridH = Rows * cell + (Rows - 1) * gap;
        float w = gridW + 64f;
        float h = gridH + 150f;

        var box = new Rect((Screen.width - w) * 0.5f, (Screen.height - h) * 0.5f, w, h);
        GUI.color = new Color(0.05f, 0.07f, 0.10f, 0.94f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);

        GUI.color = Color.white;
        GUI.Label(new Rect(box.x, box.y + 14f, box.width, 34f), "<b>CHOOSE YOUR KAIJU</b>", titleStyle);

        float gx = box.center.x - gridW * 0.5f;
        float gy = box.y + 58f;
        for (int i = 0; i < Cells; i++)
            cellRects[i] = new Rect(gx + (i % Cols) * (cell + gap),
                                    gy + (i / Cols) * (cell + gap), cell, cell);

        if (mouseMoved)
            for (int i = 0; i < Cells; i++)
                if (cellRects[i].Contains(Event.current.mousePosition)) highlighted = i;

        for (int i = 0; i < Cells; i++) DrawCell(i, cellRects[i]);

        // The highlighted name sits under the grid rather than under each portrait: at this
        // cell size a caption per tile is unreadable, and only one of them is ever the
        // answer to "what am I about to pick".
        var chosen = CharacterAt(highlighted);
        GUI.color = chosen != null && chosen.unlocked ? Color.white : new Color(1f, 1f, 1f, 0.5f);
        GUI.Label(new Rect(box.x, gy + gridH + 8f, box.width, 24f),
                  chosen != null && chosen.unlocked ? $"<b>{chosen.displayName}</b>" : "Locked",
                  labelStyle);

        GUI.color = new Color(1f, 1f, 1f, 0.45f);
        GUI.Label(new Rect(box.x, box.y + h - 34f, box.width, 18f),
                  "Move with WASD, arrows, numpad or the left stick", smallStyle);
        GUI.Label(new Rect(box.x, box.y + h - 20f, box.width, 18f),
                  "Enter, Space, A or RT to choose   ·   Esc or B to go back", smallStyle);
    }

    void DrawCell(int i, Rect r)
    {
        var c = CharacterAt(i);
        bool playable = c != null && c.unlocked;
        bool on = i == highlighted;

        // The frame is drawn as a border rather than a glow: a locked tile is nearly black,
        // and a glow behind one is invisible.
        if (on)
        {
            GUI.color = new Color(1f, 0.66f, 0.24f, 0.95f);
            GUI.DrawTexture(new Rect(r.x - 3f, r.y - 3f, r.width + 6f, r.height + 6f), Texture2D.whiteTexture);
        }

        GUI.color = playable ? new Color(0.13f, 0.16f, 0.21f, 1f) : new Color(0.02f, 0.02f, 0.03f, 1f);
        GUI.DrawTexture(r, Texture2D.whiteTexture);

        if (playable)
        {
            var portrait = PortraitFor(i, c);
            if (portrait != null)
            {
                GUI.color = c.tint;
                float span = 1f / Mathf.Max(1f, c.portraitZoom);
                GUI.DrawTextureWithTexCoords(r, portrait,
                    new Rect((1f - span) * 0.5f, (1f - span) * 0.5f, span, span));
            }
            else
            {
                // Art not baked yet, or the package is missing. The name still says which
                // slot this is, so the grid stays usable on greybox.
                GUI.color = Color.white;
                GUI.Label(r, c.displayName, smallStyle);
            }
        }
        else
        {
            GUI.color = new Color(1f, 1f, 1f, 0.22f);
            GUI.Label(r, "?", lockStyle);
        }

        GUI.color = Color.white;
        if (GUI.Button(r, GUIContent.none, GUIStyle.none)) ChooseCharacter(i);
    }

    /// <summary>
    /// Portraits are loaded once and kept. Resources.Load is cheap on a repeat, but OnGUI
    /// runs several times a frame — twice for layout and repaint alone — and this would be
    /// twenty lookups every one of them.
    /// </summary>
    Texture2D PortraitFor(int i, CharacterType c)
    {
        if (i < 0 || i >= Cells) return null;

        portraits ??= new Texture2D[Cells];
        portraitLoaded ??= new bool[Cells];

        // The flag rather than a null check on the slot: a character whose art has not been
        // baked resolves to null, and testing the slot alone would retry that lookup on
        // every repaint forever.
        if (!portraitLoaded[i])
        {
            portraits[i] = CharacterArt.Portrait(c);
            portraitLoaded[i] = true;
        }
        return portraits[i];
    }
}
