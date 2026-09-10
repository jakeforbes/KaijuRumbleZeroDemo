using System;
using UnityEngine;
using UnityEngine.Audio;

public enum MusicState { DefaultGame, BossApproaching, BossFight }

[CreateAssetMenu(menuName = "KRZ/Music Settings", fileName = "Music Settings")]
public sealed class MusicSettings : ScriptableObject
{
    [Serializable]
    public class Track
    {
        public AudioClip clip;
        [Range(0, 1)] public float volume = 1;
    }

    [Range(0, 1)] public float masterVolume = 0.6f;
    [Min(0)] public float crossfadeSeconds = 2;
    public AudioMixerGroup output;
    [Tooltip("Both gameplay positions must enter the camera frame after the introduction. Sprite padding and shadows do not affect this check. This inset avoids triggering on the very edge of the screen.")]
    [Range(0, 0.2f)] public float bossFightScreenMargin = 0.03f;
    public Track defaultGame = new();
    public Track bossApproaching = new();
    public Track bossFight = new();

    public Track TrackFor(MusicState state)
    {
        var track = state == MusicState.BossApproaching ? bossApproaching
                  : state == MusicState.BossFight ? bossFight : defaultGame;
        // Unassigned boss tracks keep normal music playing.
        return track != null && track.clip != null ? track : defaultGame;
    }
}
