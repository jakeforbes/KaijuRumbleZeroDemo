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
    float fps;

    void Update()
    {
        fps = Mathf.Lerp(fps, 1f / Mathf.Max(Time.unscaledDeltaTime, 0.0001f), 0.1f);
        if (tuning.enableCheatKeys) ReadCheats();
    }

    void ReadCheats()
    {
        var kb = Keyboard.current;
        if (kb == null) return;

        if (kb.f1Key.wasPressedThisFrame) tuning.showDebugHud = !tuning.showDebugHud;
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
            $"scale  {player.Scale:0.00}×   zoom  {(cam != null ? cam.orthographicSize : 0f):0.00}\n" +
            $"\n<b>F1</b> hud   <b>F2/F3</b> size ±   <b>F10</b> restart   <b>[ ]</b> timescale   <b>\\</b> reset";

        var size = style.CalcSize(new GUIContent(text));
        var rect = new Rect(12, 12, size.x + 16, size.y + 10);

        GUI.color = new Color(0f, 0f, 0f, 0.55f);
        GUI.DrawTexture(rect, Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(rect, text, style);
    }
}
