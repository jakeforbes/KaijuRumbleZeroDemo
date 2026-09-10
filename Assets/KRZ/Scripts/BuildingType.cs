using UnityEngine;

/// <summary>
/// One class of building. There are five, matching the five kaiju sizes: a building
/// of your own class takes a couple of seconds, anything above you climbs steeply.
///
/// Footprints deliberately vary in shape as well as area — a 2x2 that is short reads
/// very differently from a 1x3 tower, and keeping some big footprints low stops the
/// skyline swallowing the play space.
/// </summary>
[System.Serializable]
public class BuildingType
{
    [Header("Hit debris")]
    public bool debrisEnabled = true;
    [Range(0, 100)] public int debrisCount = 15;
    [Tooltip("Fragment size multiplier range.")]
    public Vector2 debrisSize = new Vector2(1.4f, 2.6f);
    [Tooltip("Outward speed range in world units per second.")]
    public Vector2 debrisForce = new Vector2(1.5f, 3.3f);
    [Range(0, 180)] public float debrisSpread = 25f;
    [Min(0.1f)] public float debrisGravity = 7f;

    [Tooltip("Tint for hit debris chips. Defaults to the same concrete-blue every " +
             "building class shares in the delivered art, rather than this type's own " +
             "accent colour, so debris reads as one consistent material across the " +
             "whole city. Override per class only where it should chip differently " +
             "(e.g. a reactor's hot innards).")]
    public Color debrisColour = new Color(0.266f, 0.286f, 0.382f);
    [Header("Building")]
    public string name = "Block";

    [Tooltip("Which kaiju size this is a fair fight for. 0 is size 1.")]
    [Range(0, 4)] public int sizeClass;

    [Tooltip("Ground footprint in tiles. Orientation is flipped at random when placed.")]
    public int tilesX = 1;
    public int tilesY = 1;

    [Tooltip("Per-axis scale applied to the generated footprint diamond, on top of the " +
             "global Building Footprint fraction. 1,1 matches the idealised tile math " +
             "exactly, which is always correct for greybox. Only delivered art whose " +
             "actual base doesn't match that math needs this touched. Calibrate with " +
             "the F12 collider overlay against the real sprite.")]
    public Vector2 footprintScale = Vector2.one;

    [Tooltip("World-unit nudge applied to the footprint after scaling, for delivered art " +
             "whose ground contact point sits off from the idealised centre. Calibrate " +
             "with the F12 collider overlay.")]
    public Vector2 footprintOffset = Vector2.zero;

    [Tooltip("Height above the footprint, in pixels. Not tied to footprint area.")]
    public int minHeightPx = 140;
    public int maxHeightPx = 200;

    [Tooltip("Set so that a kaiju of the matching size needs about three swipes.")]
    public float hp = 30f;

    public int foodDrops = 6;
    public float foodScatter = 2.5f;

    [Tooltip("Power-ups left behind when destroyed. Laboratories only — it is what " +
             "makes them worth crossing the map for rather than smashing whatever " +
             "happens to be nearest.")]
    public int upgradeDrops;

    [Header("Reactor pulse")]
    [Tooltip("Damage dealt to every enemy in radius when this is destroyed. 0 for " +
             "ordinary buildings. Hits enemies only — never other buildings, never " +
             "the player, so a reactor is a weapon rather than a hazard.")]
    public float pulseDamage;

    [Tooltip("Reach of the pulse, measured on the flat ground plane. Sized to cover " +
             "the whole screen even at maximum zoom-out.")]
    public float pulseRadius = 24f;

    [Tooltip("Reactors are placed deliberately and spaced apart rather than rolled " +
             "from the weight table, so two can never be on screen together.")]
    public bool isReactor;

    [Tooltip("Exactly one of these exists per run, placed before any other reactor. " +
             "For landmarks that should be a destination rather than a fixture.")]
    public bool unique;

    [Tooltip("Relative chance of being picked when the city is generated.")]
    public float weight = 30f;

    [Tooltip("Resources path stem for this building's art, e.g. \"Buildings/reactor_1x1\". " +
             "Four states are looked for beside it — _pristine, _damaged_1, _damaged_2 " +
             "and _destroyed — and any that are missing fall back. Blank leaves this " +
             "type on greybox.\n\n" +
             "The art is authored at 256x128 per tile, which is exactly this project's " +
             "scale, so it needs no scaling. A bare file with no suffix is still " +
             "accepted as the pristine state, which is how the first delivery landed.")]
    public string artSprite = "";

    [Tooltip("How many of the pack's five rotations this type may use, picked per " +
             "building. Free variety in a city that would otherwise repeat ten " +
             "silhouettes across two hundred blocks.\n\n" +
             "Square footprints can take all five, because turning the model does not " +
             "change its ground diamond. Non-square ones stay at 1: a quarter turn " +
             "would swap their footprint out from under the collider, and the random " +
             "flip already gives those two orientations.")]
    [Range(1, 5)] public int artDirections = 1;

    [Tooltip("Uniform scale on the delivered sprite, to sit its base plate on the " +
             "collider's tile diamond. The art is drawn with a wider ground plate than " +
             "the tile maths gives — by a different amount per building, so this is one " +
             "number each rather than a single projection fix.\n\n" +
             "Applied through the sprite's pixels-per-unit, so it scales about the " +
             "pivot and never touches the collider. Check it with the F12 overlay: the " +
             "drawn base should sit just outside the diamond, never inside it.")]
    [Range(0.2f, 2.5f)] public float artScale = 1f;

    [Tooltip("Sideways nudge of the sprite against its collider, in texture pixels, " +
             "positive moving the building left. For art whose base is not drawn in " +
             "the middle of its canvas — two of the civilians are tens of pixels off " +
             "centre, which no amount of scaling corrects because the building is not " +
             "the wrong size, it is simply standing beside its own footprint.")]
    public float artPivotOffsetX;

    [Tooltip("Vertical nudge of the sprite against its collider, in texture pixels, " +
             "positive moving the building UP. For art whose ground contact is not " +
             "where the fixed 8px margin says it is. Use this only after the scale is " +
             "right: a building the wrong size looks vertically wrong too, and nudging " +
             "it only hides the real fault.")]
    public float artPivotOffsetY;

    [Tooltip("How dark the sprite goes at the last damage step when only a pristine " +
             "render exists. Ignored once real damage states are delivered.")]
    [Range(0f, 1f)] public float damagedTint = 0.55f;

    public Color colour = new Color(0.24f, 0.28f, 0.40f);

    public bool AllowsFlip => tilesX != tilesY;
}
