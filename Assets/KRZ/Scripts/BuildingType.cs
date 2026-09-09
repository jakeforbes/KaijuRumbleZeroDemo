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
    public string name = "Block";

    [Tooltip("Which kaiju size this is a fair fight for. 0 is size 1.")]
    [Range(0, 4)] public int sizeClass;

    [Tooltip("Ground footprint in tiles. Orientation is flipped at random when placed.")]
    public int tilesX = 1;
    public int tilesY = 1;

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

    [Tooltip("Relative chance of being picked when the city is generated.")]
    public float weight = 30f;

    public Color colour = new Color(0.24f, 0.28f, 0.40f);

    public bool AllowsFlip => tilesX != tilesY;
}
