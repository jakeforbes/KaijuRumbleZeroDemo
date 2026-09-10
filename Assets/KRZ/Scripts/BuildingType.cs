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

    [Tooltip("Resources path to a pristine still, e.g. \"Buildings/laboratory\". Blank " +
             "leaves this type on greybox. The art is authored at 256x128 per tile, " +
             "which is exactly this project's scale, so it needs no scaling.")]
    public string artSprite = "";

    [Tooltip("How much the sprite darkens when damaged. The delivered art is pristine " +
             "only, so the middle state is a tint rather than a second render.")]
    [Range(0f, 1f)] public float damagedTint = 0.55f;

    public Color colour = new Color(0.24f, 0.28f, 0.40f);

    public bool AllowsFlip => tilesX != tilesY;
}
