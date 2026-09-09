using UnityEngine;

/// <summary>
/// Every tunable number in the prototype. Lives as a single asset at
/// Assets/KRZ/Resources/Tuning.asset so it can be edited in the Inspector
/// while the game is running. The arena builder never overwrites it.
/// </summary>
[CreateAssetMenu(menuName = "KRZ/Tuning", fileName = "Tuning")]
public class Tuning : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 70f;
    public float deceleration = 90f;

    [Tooltip("Vertical movement multiplier. 0.5 matches the 2:1 isometric projection. " +
             "1.0 makes it feel like a flat top-down game.")]
    [Range(0.2f, 1f)] public float isoSquash = 0.5f;

    [Header("Camera")]
    public float pixelsPerUnit = 128f;

    [Tooltip("Orthographic half-height. 4.21875 shows sprites 1:1 at 1080p with 128 PPU.")]
    public float baseOrthoSize = 4.21875f;

    [Tooltip("Fraction of the screen the player can move within before the camera follows.")]
    [Range(0f, 0.45f)] public float deadZoneX = 0.20f;
    [Range(0f, 0.45f)] public float deadZoneY = 0.16f;

    [Tooltip("Seconds for the camera to catch up. Higher is looser.")]
    public float followLag = 0.18f;

    [Tooltip("Camera zooms out by (player scale ^ this). 0.5 is square root, 0 is no zoom, " +
             "1.0 fully cancels the growth fantasy.")]
    [Range(0f, 1f)] public float zoomExponent = 0.5f;

    [Header("Arena")]
    public int blocksX = 8;
    public int blocksY = 8;

    [Tooltip("World units between block centres. Y should be about half of X to look square in iso.")]
    public float blockSpacingX = 7f;
    public float blockSpacingY = 3.5f;
    public int randomSeed = 1337;

    [Header("Footprints")]
    [Tooltip("Building collision diamond as a fraction of its drawn base. " +
             "1.0 matches the art exactly. Below 1.0 lets the player creep onto the base.")]
    [Range(0.5f, 1.2f)] public float buildingFootprint = 1f;

    [Tooltip("Player collision ellipse in world units, at size 1. Tile is 2 x 1.")]
    public Vector2 playerFootprint = new Vector2(1.1f, 0.55f);

    [Header("Occluder fade")]
    [Tooltip("Fade buildings that are drawn in front of the player and covering them.")]
    public bool occluderFadeEnabled = true;

    [Tooltip("How transparent a covering building becomes. Lower is more see-through.")]
    [Range(0.1f, 1f)] public float occluderAlpha = 0.40f;

    [Tooltip("Seconds to fade in and out. Too fast reintroduces the pop, too slow smears.")]
    [Range(0.02f, 0.6f)] public float occluderFadeTime = 0.12f;

    [Header("Player size (previewed here, driven by growth in Stage 3)")]
    [Range(1f, 4f)] public float previewScale = 1f;

    [Header("Debug")]
    public bool showDebugHud = true;
    public bool enableCheatKeys = true;
    public bool showColliders = false;
}
