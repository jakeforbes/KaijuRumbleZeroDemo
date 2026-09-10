using System.Collections.Generic;
using UnityEngine;

/// <summary>Scene-owned playback service. Templates describe sounds; this owns the actual speakers.</summary>
[DisallowMultipleComponent]
public sealed class RuntimeSoundPlayer : MonoBehaviour
{
    public static RuntimeSoundPlayer Instance { get; private set; }
    [SerializeField, Range(8, 128)] int voiceLimit = 64;
    [SerializeField] int activeVoices;
    [SerializeField] int soundsPlayed;
    [SerializeField] AudioClip lastClip;
    [SerializeField] Sfx lastAction;

    sealed class Voice
    {
        public AudioSource source;
        public AudioHighPassFilter high;
        public AudioLowPassFilter low;
        public AudioEchoFilter echo;
        public AudioReverbFilter reverb;
        public SoundPlayer owner;
        public double endsAt;
        public long sequence;
        public bool active;
        public Sfx action;
    }

    readonly List<Voice> voices = new();
    long sequence;
    public int SoundsPlayed => soundsPlayed;
    public int ActiveVoices => activeVoices;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() => Instance = null;

    public static RuntimeSoundPlayer Ensure()
    {
        if (Instance != null) return Instance;
        var existing = FindAnyObjectByType<RuntimeSoundPlayer>();
        if (existing != null) { Instance = existing; return existing; }
        return new GameObject("Sound Player").AddComponent<RuntimeSoundPlayer>();
    }

    void Awake()
    {
        if (Instance != null && Instance != this) { Destroy(gameObject); return; }
        Instance = this;
        // Visible AudioSources exist immediately, even before the first action.
        for (int i = 0; i < 8; i++) CreateVoice();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    Voice CreateVoice()
    {
        var go = new GameObject("Voice " + (voices.Count + 1));
        go.transform.SetParent(transform, false);
        var voice = new Voice
        {
            source = go.AddComponent<AudioSource>(),
            high = go.AddComponent<AudioHighPassFilter>(),
            low = go.AddComponent<AudioLowPassFilter>(),
            echo = go.AddComponent<AudioEchoFilter>(),
            reverb = go.AddComponent<AudioReverbFilter>()
        };
        voice.source.playOnAwake = false;
        voice.source.dopplerLevel = 0;
        voice.high.enabled = voice.low.enabled = voice.echo.enabled = voice.reverb.enabled = false;
        voices.Add(voice);
        return voice;
    }

    static bool IsRoutine(Sfx action) => action == Sfx.Footstep || action == Sfx.FoodPickup;

    Voice Acquire(SoundPlayer owner, Sfx action)
    {
        Voice oldestOwned = null, oldest = null, free = null;
        int owned = 0;
        foreach (var voice in voices)
        {
            if (!voice.active) { if (free == null) free = voice; continue; }
            bool canReplace = !IsRoutine(action) || IsRoutine(voice.action);
            if (canReplace && (oldest == null || voice.sequence < oldest.sequence)) oldest = voice;
            if (owner != null && voice.owner == owner)
            {
                owned++;
                if (canReplace && (oldestOwned == null || voice.sequence < oldestOwned.sequence)) oldestOwned = voice;
            }
        }
        if (owner != null && owned >= Mathf.Max(1, owner.maxVoices)) return oldestOwned;
        return free ?? (voices.Count < voiceLimit ? CreateVoice() : oldest);
    }

    public void Play(SoundPlayer owner, SoundPlayer.PlaybackSettings settings, AudioClip clip, Vector3 position, float gain = 1, Sfx action = default)
    {
        if (clip == null) return;
        if (clip.loadState == AudioDataLoadState.Failed)
        {
            Debug.LogWarning("Sound Player could not load " + clip.name, clip);
            return;
        }
        var voice = Acquire(owner, action);
        if (voice == null) return;
        var source = voice.source;
        source.Stop();
        source.transform.position = position;
        source.clip = clip;
        source.outputAudioMixerGroup = owner != null ? owner.output : null;
        source.volume = Mathf.Clamp01((owner != null ? owner.masterVolume : 1) * settings.volume * gain);
        float low = Mathf.Clamp(Mathf.Min(settings.pitchRange.x, settings.pitchRange.y), 0.1f, 3);
        float high = Mathf.Clamp(Mathf.Max(settings.pitchRange.x, settings.pitchRange.y), low, 3);
        float pitch = settings.randomizePitch ? Random.Range(low, high) : settings.pitch;
        source.pitch = Mathf.Clamp(pitch * Mathf.Pow(2, -settings.size * 0.4f), 0.1f, 3);
        source.spatialBlend = owner != null ? owner.spatialBlend : 0;
        source.minDistance = owner != null ? Mathf.Max(0.01f, owner.minDistance) : 1;
        source.maxDistance = owner != null ? Mathf.Max(source.minDistance, owner.maxDistance) : 40;
        voice.high.enabled = settings.size < 0;
        voice.low.enabled = settings.size > 0;
        voice.high.cutoffFrequency = Mathf.Lerp(10, 2200, -settings.size);
        voice.low.cutoffFrequency = Mathf.Lerp(22000, 1200, settings.size);
        voice.echo.enabled = settings.echo;
        voice.reverb.enabled = settings.reverb;
        float tail = 0;
        if (settings.echo)
        {
            voice.echo.delay = Mathf.Clamp(settings.echoDelay, 10, 1000);
            voice.echo.decayRatio = Mathf.Clamp(settings.echoDecay, 0, 0.9f);
            voice.echo.wetMix = Mathf.Clamp01(settings.echoWet);
            voice.echo.dryMix = 1;
            tail = voice.echo.delay / 1000 * (voice.echo.decayRatio > 0
                ? Mathf.Ceil(Mathf.Log(0.001f) / Mathf.Log(voice.echo.decayRatio)) : 1);
        }
        if (settings.reverb)
        {
            voice.reverb.reverbPreset = settings.reverbPreset;
            tail += voice.reverb.decayTime + voice.reverb.reverbDelay;
        }
        voice.owner = owner;
        voice.action = action;
        voice.endsAt = AudioSettings.dspTime + clip.length / source.pitch + tail + 0.1f;
        voice.sequence = ++sequence;
        voice.active = true;
        source.Play();
        soundsPlayed++;
        lastClip = clip;
        lastAction = action;
    }

    void Update()
    {
        activeVoices = 0;
        foreach (var voice in voices)
        {
            if (!voice.active) continue;
            if (AudioSettings.dspTime >= voice.endsAt)
            {
                voice.source.Stop();
                voice.source.clip = null;
                voice.owner = null;
                voice.active = false;
                continue;
            }
            activeVoices++;
        }
    }
}

