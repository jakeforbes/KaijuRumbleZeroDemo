using UnityEngine;

/// <summary>
/// Every sound the game will make, named up front. Gameplay code calls
/// AudioEvents.Play(Sfx.X) and never touches an AudioSource, so sounds can be
/// dropped in later without editing a single gameplay script.
///
/// Until clips are assigned this logs when logEvents is on, which doubles as a
/// way to see whether events fire at the right moments before any audio exists.
/// </summary>
public enum Sfx
{
    Footstep,
    BuildingHit,
    BuildingDestroyed,
    FoodPickup,
    Swipe,
    SwipeHit,
    Blast,
    GrowTier,
    Shrink,
    PlayerHit,
    Squish,
    UpgradePickup,
    EnemySpawn,
    EnemyDeath,
    BossArrive,
    Win,
    Lose,
    EnemyAttack,
    EnemyHit,
    EnemyBlocked,
    EnemyPushed,
    SwipeHitEnemy,
    SwipeHitBuilding,
    // Value 23 was Collect; reserved for migration of existing sound settings.
    // New entries go at the end: these values are serialized into the Sound Player
    // templates, so inserting mid-enum would silently repoint every clip assignment.
    EnemyDeploy = 24,
    ReactorPulse,
    BuildingStageChanged,
    BossRoar,
    MechPunch,
    KaijuImpact,
    Swarm,
    LaserSweep,
    Dash
}

public static class AudioEvents
{
    public static bool logEvents = false;

    static readonly System.Collections.Generic.Dictionary<Sfx, AudioClip> clips = new();

    public static void Register(Sfx id, AudioClip clip) => clips[id] = clip;

    /// <returns>True when configured, including intentional mute/cooldown; false when unassigned.</returns>
    public static bool Play(Sfx id, Vector3 at, GameObject owner) => Play(id, at, 1f, owner);

    // Requiring an owner makes accidental SoundPlayer bypasses a compile error.
    public static bool Play(Sfx id, Vector3 at, float volume, GameObject owner)
    {
        if (logEvents) Debug.Log($"[sfx] {id}");

        if (owner != null && owner.TryGetComponent<SoundPlayer>(out var player) && player.TryPlay(id, volume)) return true;

        if (!clips.TryGetValue(id, out var clip) || clip == null) return false;

        RuntimeSoundPlayer.Ensure().Play(null, new SoundPlayer.PlaybackSettings(), clip, at, volume, id);
        return true;
    }
}
