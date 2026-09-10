using UnityEngine;

/// <summary>Where a group arrives relative to the player.</summary>
public enum SpawnShape
{
    /// <summary>All together on one side. Reads as a squad, and can cut off a direction.</summary>
    Clump,

    /// <summary>Spread evenly all around. Use sparingly — it surrounds rather than pressures.</summary>
    Ring,

    /// <summary>In front of where the player is heading, so running forward costs something.</summary>
    Ahead,
}

/// <summary>
/// One beat of the run. A single entry covers both a one-off arrival and a sustained
/// trickle, so the whole three-minute arc is a list of these rather than code.
/// </summary>
[System.Serializable]
public class WaveEntry
{
    [Tooltip("Shown on the debug HUD when this beat is active.")]
    public string label = "wave";

    [Tooltip("Seconds into the run when this first fires.")]
    public float startTime;

    [Tooltip("Seconds into the run when it stops repeating. Leave at 0 for a one-off.")]
    public float endTime;

    [Tooltip("Seconds between repeats. 0 means fire once at startTime.")]
    public float interval;

    [Tooltip("Enemy type name, matched against Tuning.enemyTypes.")]
    public string enemyType = "Grunt";

    public int count = 4;

    public SpawnShape shape = SpawnShape.Clump;

    [Tooltip("Include a Commander every Nth time this beat fires, counting the first. " +
             "3 means the first clump and every third after. 0 means never. Deliberate " +
             "rather than a random roll, so the rhythm is something you can learn.")]
    public int commanderEvery;

    [Header("Veteran substitution")]
    [Tooltip("A tougher variant that progressively replaces the base type as the run " +
             "goes on, so the infantry line escalates without a new wave appearing.")]
    public string veteranType = "";

    [Tooltip("Firing index where substitution begins. 2 is the third squad.")]
    public int veteranFromFire = 2;

    [Tooltip("Fraction replaced on the first substituted squad.")]
    [Range(0f, 1f)] public float veteranStartFraction = 0.3f;

    [Tooltip("Extra fraction replaced each squad after, up to all of them.")]
    [Range(0f, 1f)] public float veteranRampPerFire = 0.1f;

    [HideInInspector] public float nextFireAt = -1f;
    [HideInInspector] public bool fired;
    [HideInInspector] public int fireIndex;
    [HideInInspector] public float lastAngle = -99f;

    public bool IsCommanderTurn =>
        commanderEvery > 0 && fireIndex % commanderEvery == 0;

    public bool IsSustained => interval > 0f && endTime > startTime;
}
