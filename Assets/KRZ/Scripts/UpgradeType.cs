using UnityEngine;

public enum UpgradeId
{
    Brawler,   // faster swipe
    Claws,     // longer swipe
    Fleet,     // faster movement
    Stomp,     // adds an AoE around the kaiju
    Beam,      // stronger, longer special
    Furnace,   // faster special
}

/// <summary>
/// A stacking upgrade. Every effect is a multiplier rather than a flat bonus, so a
/// pickup keeps its worth as size inflates the base stats — a flat +2 damage is
/// noise once base damage has doubled.
/// </summary>
[System.Serializable]
public class UpgradeType
{
    public UpgradeId id;
    public string displayName = "Upgrade";

    [Tooltip("Shown on the HUD. Say what it does, not how it is implemented.")]
    public string effect = "";

    [Tooltip("Multiplier applied once per stack. Below 1 for anything that shortens " +
             "a cooldown, above 1 for anything that grows.")]
    public float perStack = 1.2f;

    [Tooltip("Extra swipes added to each activation, per stack. One stack turns " +
             "X...X...X into XX...XX...XX rather than making X bigger.")]
    public int extraHitsPerStack;

    public int maxStacks = 5;

    [Tooltip("Relative chance of being the one that drops.")]
    public float weight = 1f;

    public Color colour = Color.white;
}
