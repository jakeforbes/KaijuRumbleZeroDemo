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

    [Tooltip("Roll Commanders into this group at the usual ratio. Grunt waves only.")]
    public bool allowCommanders = true;

    [HideInInspector] public float nextFireAt = -1f;
    [HideInInspector] public bool fired;

    public bool IsSustained => interval > 0f && endTime > startTime;
}
