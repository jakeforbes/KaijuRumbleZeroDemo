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
    Lose
}

public static class AudioEvents
{
    public static bool logEvents = false;

    static readonly System.Collections.Generic.Dictionary<Sfx, AudioClip> clips = new();
    static AudioSource source;

    public static void Register(Sfx id, AudioClip clip) => clips[id] = clip;

    public static void Play(Sfx id, Vector3 at = default, float volume = 1f)
    {
        if (logEvents) Debug.Log($"[sfx] {id}");

        if (!clips.TryGetValue(id, out var clip) || clip == null) return;

        if (source == null)
        {
            var go = new GameObject("~AudioEvents");
            Object.DontDestroyOnLoad(go);
            source = go.AddComponent<AudioSource>();
            source.playOnAwake = false;
        }

        source.PlayOneShot(clip, volume);
    }
}
