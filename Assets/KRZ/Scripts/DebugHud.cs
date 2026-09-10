using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// On-screen readout and cheat keys. Present from Stage 1 so testing the end of a
/// run never requires playing the start of one. Stripped from a build by turning
/// off showDebugHud / enableCheatKeys on the Tuning asset.
/// </summary>
public class DebugHud : MonoBehaviour
{
    public Tuning tuning;
    public PlayerController player;

    GUIStyle style;
    GUIStyle meterStyle;
    float fps;

    void Update()
    {
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
        Popups.show = tuning.showDamageNumbers;
        if (tuning.enableCheatKeys) ReadCheats();
        if (tuning.showColliders) DrawColliders();
    }

    /// <summary>
    /// Outlines every 2D footprint so collision can be compared against the art.
    /// Uses Debug.DrawLine, so it shows in the Scene view and in the Game view
    /// with the Gizmos toggle switched on.
    /// </summary>
    static void DrawColliders()
    {
        var green = new Color(0.3f, 1f, 0.4f);
        var cyan = new Color(0.3f, 0.9f, 1f);

        foreach (var col in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
        {
            Vector3 origin = col.transform.position;

            if (col is PolygonCollider2D poly)
            {
                var p = poly.points;
                for (int i = 0; i < p.Length; i++)
                    Debug.DrawLine(origin + (Vector3)p[i],
                                   origin + (Vector3)p[(i + 1) % p.Length], green);
            }
            else if (col is CapsuleCollider2D cap)
            {
                Vector2 half = cap.size * 0.5f;
                Vector3 prev = default;
                for (int i = 0; i <= 24; i++)
                {
                    float a = i / 24f * Mathf.PI * 2f;
                    var pt = origin + new Vector3(Mathf.Cos(a) * half.x, Mathf.Sin(a) * half.y, 0f);
                    if (i > 0) Debug.DrawLine(prev, pt, cyan);
                    prev = pt;
                }
            }
        }
    }

    void ReadCheats()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame) tuning.showDebugHud = !tuning.showDebugHud;
        if (kb.f12Key.wasPressedThisFrame) tuning.showColliders = !tuning.showColliders;
        if (kb.f10Key.wasPressedThisFrame) { GameBootstrap.Restart(); return; }
        if (CameraRig.IsBossIntroductionPlaying) return;

        var progress = PlayerProgress.Instance;
        if (progress != null)
        {
            if (kb.f2Key.wasPressedThisFrame) progress.GrowTier();
            if (kb.f3Key.wasPressedThisFrame) progress.ShrinkTier();
            if (kb.f6Key.wasPressedThisFrame) progress.godMode = !progress.godMode;
        }

        var up = PlayerUpgrades.Instance;
        if (up != null && kb.f7Key.wasPressedThisFrame)
        {
            var rolled = up.RollDrop();
            if (rolled != null) up.Grant(rolled.id);
        }

        if (GameBootstrap.Instance != null)
        {
            // Shift picks the heavier species, so armour can be tested without waves.
            bool heavy = kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed;
            if (kb.f4Key.wasPressedThisFrame) GameBootstrap.Instance.SpawnSwarm(heavy ? 4 : 12, heavy ? 1 : 0);
            if (kb.f5Key.wasPressedThisFrame) Enemy.KillAll();

            // Direct spawns for anything the swarm key cannot reach.
            if (kb.f11Key.wasPressedThisFrame)
                GameBootstrap.Instance.SpawnOne(heavy ? "Mech" : "Commander");
            if (kb.f9Key.wasPressedThisFrame) GameBootstrap.Instance.SpawnOne("Abomination");
            if (kb.f8Key.wasPressedThisFrame)
                GameBootstrap.Instance.SpawnOne(heavy ? "Scavenger" : "Dropship");
        }

        // Timeline scrub. Testing the boss should not require playing three minutes.
        var wd = WaveDirector.Instance;
        if (wd != null)
        {
            if (kb.periodKey.wasPressedThisFrame) wd.Skip(30f);
            if (kb.commaKey.wasPressedThisFrame) wd.Skip(-15f);
        }

        if (CameraRig.IsBossIntroductionPlaying) return;
        if (kb.leftBracketKey.wasPressedThisFrame)
            Time.timeScale = Mathf.Max(0.1f, Time.timeScale - 0.25f);
        if (kb.rightBracketKey.wasPressedThisFrame)
            Time.timeScale = Mathf.Min(3f, Time.timeScale + 0.25f);
        if (kb.backslashKey.wasPressedThisFrame) Time.timeScale = 1f;
    }

    void OnGUI()
    {
        DrawFoodMeter();
        if (!tuning.showDebugHud) return;

        style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            richText = true,
            padding = new RectOffset(10, 10, 6, 6)
        };

        var cam = Camera.main;
        string text =
            $"<b>KRZ</b>   {fps:0} fps   timescale {Time.timeScale:0.00}\n" +
            $"pos  {player.transform.position.x:0.0}, {player.transform.position.y:0.0}\n" +
            $"speed  {player.Velocity.magnitude:0.00}   facing  {player.FacingName}\n" +
            $"size  {(PlayerProgress.Instance != null ? PlayerProgress.Instance.SizeNumber : 1)}" +
            $"   hp  {(PlayerProgress.Instance != null ? PlayerProgress.Instance.Hp : 0f):0}" +
            $"/{(PlayerProgress.Instance != null ? PlayerProgress.Instance.MaxHp : 0f):0}" +
            $"   food  {(PlayerProgress.Instance != null ? PlayerProgress.Instance.FoodTotal : 0f):0} total\n" +
            $"scale  {player.Scale:0.00}×   zoom  {(cam != null ? cam.orthographicSize : 0f):0.00}\n" +
            $"enemies  {Enemy.All.Count} / {tuning.maxEnemiesAlive}" +
            $"{(PlayerProgress.Instance != null && PlayerProgress.Instance.godMode ? "   <b>GOD</b>" : "")}\n" +
            $"run  {RunClock()}   wave  {(WaveDirector.Instance != null ? WaveDirector.Instance.CurrentLabel : "-")}" +
            $"   resist  {(PlayerProgress.Instance != null ? PlayerProgress.Instance.Resistance : 0f):0}" +
            $"{(WaveDirector.Instance != null && WaveDirector.Instance.Recovering ? $"   <b>RECOVERING {WaveDirector.Instance.RecoverySecondsLeft:0}s</b>" : "")}\n" +
            $"\n<b>F1</b> hud   <b>F2/F3</b> size ±   <b>F4</b> swarm (+shift heavy)   <b>F5</b> kill all" +
            $"\n<b>F6</b> god   <b>F7</b> upgrade   <b>F10</b> restart   <b>F8</b> dropship (+shift scav)   <b>F9</b> boss   <b>F11</b> cmdr (+shift mech)   <b>F12</b> colliders   <b>. ,</b> skip time   <b>[ ]</b> speed" +
            $"\n<b>Space / E / pad A</b> blast";

        var size = style.CalcSize(new GUIContent(text));
        var rect = new Rect(12, 12, size.x + 16, size.y + 10);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, text, style);
    }

    static string RunClock()
    {
        float t = WaveDirector.Instance != null ? WaveDirector.Instance.Clock : 0f;
        return $"{Mathf.FloorToInt(t / 60f)}:{Mathf.FloorToInt(t % 60f):00}";
    }

    /// <summary>
    /// Real HUD, not debug output: growth needs visible progress between bites or
    /// most of a run reads as nothing happening. Stays on when the debug HUD is off.
    /// </summary>
    void DrawFoodMeter()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null) return;

        meterStyle ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 13,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };

        const float w = 420f, h = 20f;
        float x = (Screen.width - w) * 0.5f;
        float y = Screen.height - h - 28f;
        var bar = new Rect(x, y, w, h);

        GUI.color = new Color(0f, 0f, 0f, 0.60f);
        GUI.DrawTexture(bar, Texture2D.whiteTexture);

        GUI.color = new Color(1f, 0.66f, 0.24f, 0.92f);
        GUI.DrawTexture(new Rect(x + 2f, y + 2f, (w - 4f) * progress.TierProgress, h - 4f),
                        Texture2D.whiteTexture);

        GUI.color = Color.white;
        string label = progress.AtMaxTier
            ? $"<b>SIZE {progress.SizeNumber}</b>   MAX"
            : $"<b>SIZE {progress.SizeNumber}</b>   {progress.FoodThisTier:0} / {progress.FoodForNextTier:0}";
        GUI.Label(bar, label, meterStyle);

        // Health sits directly above the food meter: the two numbers that decide a run.
        const float hh = 12f;
        float hy = y - hh - 5f;
        var hbar = new Rect(x, hy, w, hh);
        float frac = progress.MaxHp > 0f ? Mathf.Clamp01(progress.Hp / progress.MaxHp) : 0f;

        GUI.color = new Color(0f, 0f, 0f, 0.60f);
        GUI.DrawTexture(hbar, Texture2D.whiteTexture);

        GUI.color = frac > 0.35f ? new Color(0.45f, 0.85f, 0.42f, 0.92f)
                                 : new Color(0.93f, 0.30f, 0.26f, 0.95f);
        GUI.DrawTexture(new Rect(x + 2f, hy + 2f, (w - 4f) * frac, hh - 4f), Texture2D.whiteTexture);
        GUI.color = Color.white;

        DrawBlastAndUpgrades(x, hy);

        if (progress.IsDead) DrawGameOver();
        else if (progress.HasWon) DrawWin(progress);
    }

    /// <summary>
    /// Flashes rather than sits there. A static box reads as the game having stopped;
    /// a pulse reads as a celebration, which is what the moment is owed.
    /// </summary>
    void DrawWin(PlayerProgress progress)
    {
        float since = Time.time - progress.WonAt;
        float pulse = Mathf.PingPong(since * 3f, 1f);

        var box = new Rect((Screen.width - 420f) * 0.5f, Screen.height * 0.34f, 420f, 104f);
        GUI.color = new Color(0.05f, 0.10f, 0.06f, 0.80f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);

        var big = new GUIStyle(meterStyle) { fontSize = 34 };
        GUI.color = Color.Lerp(new Color(0.55f, 1f, 0.6f), Color.white, pulse);
        GUI.Label(new Rect(box.x, box.y + 16f, box.width, 44f), "<b>YOU WIN!</b>", big);

        GUI.color = Color.white;
        GUI.Label(new Rect(box.x, box.y + 62f, box.width, 24f),
                  $"the Abomination is down   —   food eaten {progress.FoodTotal:0}   —   F10 to restart",
                  meterStyle);
    }

    /// <summary>Blast readiness on the left, collected upgrades stacked beside it.</summary>
    void DrawBlastAndUpgrades(float x, float meterY)
    {
        var special = player != null ? player.GetComponent<PlayerSpecial>() : null;
        var up = PlayerUpgrades.Instance;

        float y = meterY - 26f;

        if (special != null)
        {
            bool ready = special.BlastCooldownRemaining <= 0f;
            float frac = ready ? 1f
                : 1f - Mathf.Clamp01(special.BlastCooldownRemaining / Mathf.Max(0.01f, special.BlastCooldownTotal));

            var box = new Rect(x, y, 120f, 18f);
            GUI.color = new Color(0f, 0f, 0f, 0.6f);
            GUI.DrawTexture(box, Texture2D.whiteTexture);
            GUI.color = ready ? new Color(0.45f, 0.85f, 1f, 0.95f) : new Color(0.3f, 0.45f, 0.6f, 0.9f);
            GUI.DrawTexture(new Rect(x + 2f, y + 2f, 116f * frac, 14f), Texture2D.whiteTexture);
            GUI.color = Color.white;
            GUI.Label(box, ready ? "<b>BLAST  ready</b>" : "BLAST", meterStyle);
        }

        if (up == null) return;

        float ux = x + 132f;
        foreach (var pair in up.Stacks)
        {
            if (pair.Value <= 0) continue;
            var type = up.Find(pair.Key);
            if (type == null) continue;

            string text = $"{type.displayName} x{pair.Value}";
            var size = meterStyle.CalcSize(new GUIContent(text));
            var chip = new Rect(ux, y, size.x + 14f, 18f);

            GUI.color = new Color(type.colour.r, type.colour.g, type.colour.b, 0.22f);
            GUI.DrawTexture(chip, Texture2D.whiteTexture);
            GUI.color = type.colour;
            GUI.Label(chip, text, meterStyle);
            GUI.color = Color.white;

            ux += chip.width + 6f;
        }
    }

    void DrawGameOver()
    {
        var box = new Rect((Screen.width - 380f) * 0.5f, Screen.height * 0.38f, 380f, 92f);
        GUI.color = new Color(0f, 0f, 0f, 0.78f);
        GUI.DrawTexture(box, Texture2D.whiteTexture);
        GUI.color = Color.white;

        var big = new GUIStyle(meterStyle) { fontSize = 26 };
        GUI.Label(new Rect(box.x, box.y + 16f, box.width, 34f), "<b>GAME OVER</b>", big);
        GUI.Label(new Rect(box.x, box.y + 52f, box.width, 24f),
                  $"food eaten {PlayerProgress.Instance.FoodTotal:0}   —   F10 to restart", meterStyle);
    }
}
