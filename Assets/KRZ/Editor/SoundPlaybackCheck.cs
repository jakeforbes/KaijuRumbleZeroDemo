using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

/// <summary>Checks the real game event route and samples the resulting audio output.</summary>
public static class SoundPlaybackCheck
{
    static double until;
    static float peak;
    static readonly float[] samples = new float[1024];

    [MenuItem("KRZ/Audio/Check Playback (Play mode) %&t")]
    static void Check()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogWarning("Enter Play mode before checking sound playback.");
            return;
        }
        var player = UnityEngine.Object.FindAnyObjectByType<PlayerController>();
        if (player == null || !player.TryGetComponent<SoundPlayer>(out var sound))
        {
            Finish("FAIL: Generated player has no SoundPlayer component.");
            return;
        }
        var manager = RuntimeSoundPlayer.Ensure();
        int before = manager.SoundsPlayed;
        AudioEvents.Play(Sfx.Footstep, player.transform.position, owner: player.gameObject);
        if (manager.SoundsPlayed == before)
        {
            Finish("FAIL: Footstep did not start. Check assigned clips, volume, enabled state and cooldown.");
            return;
        }
        Selection.activeGameObject = manager.gameObject;
        peak = 0;
        until = EditorApplication.timeSinceStartup + 1.5;
        EditorApplication.update -= Sample;
        EditorApplication.update += Sample;
    }

    static void Sample()
    {
        if (!EditorApplication.isPlaying) { EditorApplication.update -= Sample; return; }
        AudioListener.GetOutputData(samples, 0);
        foreach (float value in samples) peak = Mathf.Max(peak, Mathf.Abs(value));
        if (EditorApplication.timeSinceStartup < until) return;
        EditorApplication.update -= Sample;
        var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsInactive.Exclude)
            .Count(x => x.isActiveAndEnabled);
        Finish((peak > 0 && listeners == 1 ? "PASS" : "FAIL") +
            ": Player Footstep reached the runtime manager. Listener output peak=" + peak +
            ", active listeners=" + listeners + ", listener volume=" + AudioListener.volume +
            ", listener paused=" + AudioListener.pause +
            ", pooled AudioSources=" + RuntimeSoundPlayer.Instance.GetComponentsInChildren<AudioSource>().Length);
    }

    static void Finish(string message)
    {
        Directory.CreateDirectory("Temp/SoundPlayerValidation");
        File.WriteAllText("Temp/SoundPlayerValidation/playback-result.txt", DateTime.Now + " " + message);
        if (message.StartsWith("PASS")) Debug.Log(message);
        else Debug.LogWarning(message);
    }
}

