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

    [Header("Growth")]
    [Tooltip("Food needed to leave each tier. One fewer entry than there are sizes. " +
             "Totals 600 across the run, the same as the four-size version did.")]
    public float[] foodPerTier = { 75f, 125f, 175f, 225f };

    [Tooltip("Size at the start of each tier. Add or remove entries to change how many " +
             "sizes exist — everything else derives from this array's length.")]
    public float[] tierScale = { 1f, 1.75f, 2.5f, 3.25f, 4f };

    [Tooltip("Health cap at each tier. Reaching a tier heals you to its cap.")]
    public float[] tierMaxHp = { 100f, 140f, 175f, 210f, 250f };

    [Tooltip("How much of the gap to the next size is covered continuously as the meter " +
             "fills, with the rest arriving as a jump at tier-up. 0 makes growth purely " +
             "stepped, which is what makes most of a run read as nothing happening.")]
    [Range(0f, 1f)] public float withinTierGrowth = 0.7f;

    [Tooltip("Damage multiplier compounded per tier. Lowered from 1.35 when a fifth size " +
             "was added, so four steps land on the same 2.45x total three steps did.")]
    public float damagePerTier = 1.25f;

    [Tooltip("Move speed multiplier compounded per tier. Growth should never feel slower. " +
             "Lowered from 1.08 for the same reason as damage.")]
    public float speedPerTier = 1.06f;

    [Tooltip("How fast the body catches up to its target size. Lower is more elastic.")]
    public float growthLerpSpeed = 7f;

    public float tierUpShake = 0.5f;

    [Header("Footprints")]
    [Tooltip("Building collision diamond as a fraction of its drawn base. " +
             "1.0 matches the art exactly. Below 1.0 lets the player creep onto the base.")]
    [Range(0.5f, 1.2f)] public float buildingFootprint = 1f;

    [Tooltip("Player collision ellipse in world units, at size 1. Tile is 2 x 1.")]
    public Vector2 playerFootprint = new Vector2(1.1f, 0.55f);

    [Header("Swipe — the auto attack")]
    public float swipeDamage = 10f;
    public float swipeCooldown = 2f;

    [Tooltip("Reach in world units at size 1. Scales with the kaiju in Stage 3.")]
    public float swipeRange = 2.2f;

    [Tooltip("Width of the hit arc in degrees, centred on facing.")]
    [Range(30f, 360f)] public float swipeArc = 130f;

    [Tooltip("Draw the swipe arc briefly. A tuning aid, replaced by real VFX in Stage 9.")]
    public bool showSwipeArc = true;

    [Header("Buildings")]
    public float buildingHpSmall = 40f;

    [Tooltip("Deliberately out of reach at size 1. Towers act as a soft gate that " +
             "opens as the kaiju grows, so the arena visibly unlocks over a run.")]
    public float buildingHpLarge = 450f;

    [Tooltip("Bonus damage against large buildings only, indexed by size. This is what " +
             "makes being giant feel giant against towers, without inflating the baseline " +
             "curve against everything else. Small buildings keep their friction.")]
    public float[] largeBuildingDamageBySize = { 1f, 2f, 3f, 4f, 5f };

    [Tooltip("Progress bar over buildings you have damaged. Intact ones show nothing.")]
    public bool showBuildingHealthBars = true;
    public int foodDropsSmall = 9;
    public int foodDropsLarge = 34;

    [Header("Food")]
    public float foodValueSmall = 1f;
    public float foodValueMedium = 5f;
    public float foodValueLarge = 20f;

    [Tooltip("Outer edge of the pull field. Food further out is untouched. " +
             "Grows with size in Stage 3.")]
    public float foodInfluenceRadius = 6.5f;

    [Tooltip("How sharply the pull falls off toward the edge. 1 is linear; higher keeps the " +
             "outer field nearly dead while ramping hard close to the body.")]
    [Range(1f, 6f)] public float foodPullFalloff = 4f;

    [Tooltip("Speed at the moment food reaches you — the top of the curve.")]
    public float foodMagnetMaxSpeed = 20f;

    [Tooltip("How quickly a piece takes up the speed the field is asking for. " +
             "Only bites near the body, where the field asks for a lot at once.")]
    public float foodMagnetAccel = 30f;

    [Tooltip("How far food is thrown by a small building, in world units.")]
    public float foodScatter = 3.4f;

    [Tooltip("How far food is thrown by a large building. Wide enough that clearing a " +
             "tower means walking the debris field.")]
    public float foodScatterLarge = 7f;

    [Tooltip("Seconds food spends arcing out before it settles.")]
    [Range(0.15f, 2f)] public float foodHopTime = 0.7f;

    [Tooltip("Seconds food sits before it will fly to you. Without this you never see it, " +
             "because you are standing next to the building you just smashed.")]
    public float foodArmDelay = 0.7f;

    [Header("Occluder fade")]
    [Tooltip("Fade buildings that are drawn in front of the player and covering them.")]
    public bool occluderFadeEnabled = true;

    [Tooltip("How transparent a covering building becomes. Lower is more see-through.")]
    [Range(0.1f, 1f)] public float occluderAlpha = 0.40f;

    [Tooltip("Seconds to fade in and out. Too fast reintroduces the pop, too slow smears.")]
    [Range(0.02f, 0.6f)] public float occluderFadeTime = 0.12f;

    [Header("Debug")]
    public bool showDebugHud = true;
    public bool enableCheatKeys = true;
    public bool showColliders = false;
}
