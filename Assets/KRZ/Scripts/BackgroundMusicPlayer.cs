using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

/// <summary>Scene-owned looping music, independent of sound-effect voices.</summary>
[DefaultExecutionOrder(100)]
public sealed class BackgroundMusicPlayer : MonoBehaviour
{
    public MusicSettings settings;
    public UnityEvent<MusicState> onMusicStateChanged = new();
    [SerializeField] MusicState currentState;
    [SerializeField] bool manualControl;
    public MusicState CurrentState => currentState;
    readonly HashSet<Enemy> engagedBosses = new();
    readonly AudioSource[] sources = new AudioSource[2];
    readonly float[] startGains = new float[2];
    readonly float[] gains = new float[2];
    int active = -1;
    float fadeTime;
    bool initialized;

    void Awake()
    {
        for (int i = 0; i < 2; i++)
        {
            var child = new GameObject("Music " + (i + 1));
            child.transform.SetParent(transform, false);
            sources[i] = child.AddComponent<AudioSource>();
            sources[i].playOnAwake = false;
            sources[i].loop = true;
            sources[i].spatialBlend = 0;
            sources[i].volume = 0;
        }
    }

    public static BackgroundMusicPlayer Create(MusicSettings config)
    {
        var player = new GameObject("Background Music Player").AddComponent<BackgroundMusicPlayer>();
        player.settings = config;
        return player;
    }

    // Hook these to UnityEvents or call them from scripted encounters.
    [ContextMenu("Music Events/Default Game")]
    public void PlayDefaultGame() { manualControl = true; SetState(MusicState.DefaultGame); }
    [ContextMenu("Music Events/Boss Approaching")]
    public void PlayBossApproaching() { manualControl = true; SetState(MusicState.BossApproaching); }
    [ContextMenu("Music Events/Boss Fight Started")]
    public void PlayBossFight() { manualControl = true; SetState(MusicState.BossFight); }
    [ContextMenu("Music Events/Resume Automatic Music")]
    public void ResumeAutomaticMusic() { manualControl = false; }

    void LateUpdate()
    {
        if (settings == null) return;
        if (CameraRig.IsBossIntroductionPlaying)
        {
            // A real boss spawn takes over from any earlier manual preview.
            manualControl = false;
            SetState(MusicState.BossApproaching);
        }
        else if (!manualControl) SetState(DetectState());
        else if (!initialized) SetState(currentState);

        var track = settings.TrackFor(currentState);
        AudioClip wanted = track != null ? track.clip : null;
        if ((active >= 0 ? sources[active].clip : null) != wanted) BeginTransition(wanted);
        fadeTime += Time.unscaledDeltaTime;
        float t = settings.crossfadeSeconds <= 0 ? 1 : Mathf.Clamp01(fadeTime / settings.crossfadeSeconds);
        float targetGain = track != null ? Mathf.Clamp01(track.volume) : 0;
        for (int i = 0; i < 2; i++)
        {
            gains[i] = Mathf.Lerp(startGains[i], i == active ? targetGain : 0, t);
            sources[i].volume = gains[i] * Mathf.Clamp01(settings.masterVolume);
            sources[i].outputAudioMixerGroup = settings.output;
            if (t >= 1 && i != active && sources[i].clip != null)
            {
                sources[i].Stop();
                sources[i].clip = null;
            }
        }
    }

    MusicState DetectState()
    {
        engagedBosses.RemoveWhere(e => e == null || !e.isActiveAndEnabled || !e.IsAlive);
        bool approaching = false;
        var player = PlayerProgress.Instance;
        var camera = Camera.main;
        bool playerVisible = player != null && CameraRig.InCombatView(camera, player.transform, settings.bossFightScreenMargin);
        foreach (var enemy in Enemy.All)
        {
            if (enemy == null || !enemy.IsAlive || !enemy.IsBoss) continue;
            approaching = true;
            if (playerVisible && CameraRig.InCombatView(camera, enemy.transform, settings.bossFightScreenMargin))
                engagedBosses.Add(enemy);
        }
        // Once a fight begins, moving away does not restart the approach track.
        return engagedBosses.Count > 0 ? MusicState.BossFight
             : approaching ? MusicState.BossApproaching : MusicState.DefaultGame;
    }

    void SetState(MusicState state)
    {
        if (!Application.isPlaying) return;
        if (initialized && currentState == state) return;
        initialized = true;
        currentState = state;
        var track = settings != null ? settings.TrackFor(state) : null;
        BeginTransition(track != null ? track.clip : null);
        onMusicStateChanged.Invoke(state);
    }

    void BeginTransition(AudioClip clip)
    {
        if (active >= 0 && sources[active].clip == clip) return;
        int next = -1;
        if (clip != null)
        {
            for (int i = 0; i < 2; i++) if (sources[i].clip == clip) next = i;
            if (next < 0)
            {
                next = gains[0] <= gains[1] ? 0 : 1;
                sources[next].Stop();
                sources[next].clip = clip;
                sources[next].volume = 0;
                gains[next] = 0;
                sources[next].Play();
            }
        }
        active = next;
        fadeTime = 0;
        for (int i = 0; i < 2; i++) startGains[i] = gains[i];
    }

    void OnDisable()
    {
        foreach (var source in sources) if (source != null) { source.Stop(); source.clip = null; }
        active = -1;
        initialized = false;
        gains[0] = gains[1] = startGains[0] = startGains[1] = 0;
    }
}
