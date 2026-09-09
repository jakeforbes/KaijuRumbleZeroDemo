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
        if (kb.f4Key.wasPressedThisFrame) tuning.showColliders = !tuning.showColliders;
        if (kb.f10Key.wasPressedThisFrame) GameBootstrap.Restart();

        // Stage 3 hands F2/F3 to the growth system; until then they preview sizes.
        if (kb.f2Key.wasPressedThisFrame)
            tuning.previewScale = Mathf.Min(4f, Mathf.Round(tuning.previewScale) + 1f);
        if (kb.f3Key.wasPressedThisFrame)
            tuning.previewScale = Mathf.Max(1f, Mathf.Round(tuning.previewScale) - 1f);

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
            $"<b>KRZ — Stage 1</b>   {fps:0} fps   timescale {Time.timeScale:0.00}\n" +
            $"pos  {player.transform.position.x:0.0}, {player.transform.position.y:0.0}\n" +
            $"speed  {player.Velocity.magnitude:0.00}   facing  {player.FacingName}\n" +
            $"food  {(PlayerProgress.Instance != null ? PlayerProgress.Instance.FoodTotal : 0f):0} total\n" +
            $"scale  {player.Scale:0.00}×   zoom  {(cam != null ? cam.orthographicSize : 0f):0.00}\n" +
            $"\n<b>F1</b> hud   <b>F2/F3</b> size ±   <b>F4</b> colliders   <b>F10</b> restart   <b>[ ]</b> timescale   <b>\\</b> reset";

        var size = style.CalcSize(new GUIContent(text));
        var rect = new Rect(12, 12, size.x + 16, size.y + 10);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, text, style);
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
        GUI.Label(bar, $"<b>FOOD</b>   {progress.FoodThisTier:0} / {progress.FoodForNextTier:0}", meterStyle);
    }
}
