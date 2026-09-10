using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

[AttributeUsage(AttributeTargets.Class, Inherited = true)]
public sealed class SoundActionsAttribute : Attribute
{
    public readonly Sfx[] actions;
    public SoundActionsAttribute(params Sfx[] actions) => this.actions = actions;
}

[DisallowMultipleComponent]
[AddComponentMenu("Audio/Sound Player")]
public class SoundPlayer : MonoBehaviour
{
    [Serializable]
    public class PlaybackSettings
    {

        public bool enabled = true;
        public AudioClip[] clips = Array.Empty<AudioClip>();
        [Range(0, 1)] public float volume = 1;
        public bool randomizePitch;
        [Range(0.1f, 3)] public float pitch = 1;
        public Vector2 pitchRange = new Vector2(0.9f, 1.1f);
        [Tooltip("Negative: smaller/tinnier (cuts bass). Positive: larger/darker (cuts treble and lowers pitch). Zero: unchanged.")]
        [Range(-1, 1)] public float size;
        public bool echo;
        [Range(10, 1000)] public float echoDelay = 150;
        [Range(0, 0.9f)] public float echoDecay = 0.35f;
        [Range(0, 1)] public float echoWet = 0.3f;
        public bool reverb;
        public AudioReverbPreset reverbPreset = AudioReverbPreset.Room;
        [Min(0)] public float cooldown;
        public PlaybackSettings CopySettings()
        {
            var copy = (PlaybackSettings)MemberwiseClone();
            copy.clips = clips == null ? Array.Empty<AudioClip>() : (AudioClip[])clips.Clone();
            return copy;
        }
    }

    [Serializable]
    public class SizeSettings
    {
        public bool overrideSettings;
        public PlaybackSettings settings = new();
    }

    [Serializable]
    public class ActionSound : PlaybackSettings
    {
        public Sfx action;
        public bool useSizeSettings;
        public SizeSettings[] sizeSettings = Array.Empty<SizeSettings>();
        [NonSerialized] public float nextPlayAt;

        // Each new level starts with a copy of the existing sound, not empty clips.
        public void InitializeSizeSettings()
        {
            var previous = sizeSettings ?? Array.Empty<SizeSettings>();
            sizeSettings = new SizeSettings[5];
            for (int i = 0; i < 5; i++)
                sizeSettings[i] = i < previous.Length && previous[i] != null ? previous[i]
                    : new SizeSettings { settings = CopyBaseSettings() };
        }

        PlaybackSettings CopyBaseSettings()
        {
            // Copy only playback fields, never the derived action/override graph.
            return new PlaybackSettings
            {
                enabled = enabled, clips = clips == null ? Array.Empty<AudioClip>() : (AudioClip[])clips.Clone(),
                volume = volume, randomizePitch = randomizePitch, pitch = pitch, pitchRange = pitchRange,
                size = size, echo = echo, echoDelay = echoDelay, echoDecay = echoDecay, echoWet = echoWet,
                reverb = reverb, reverbPreset = reverbPreset, cooldown = cooldown
            };
        }

        public PlaybackSettings SettingsForSize(int sizeNumber)
        {
            if (!useSizeSettings || sizeNumber < 1 || sizeNumber > 5 || sizeSettings == null
                || sizeNumber > sizeSettings.Length) return this;
            var level = sizeSettings[sizeNumber - 1];
            return level != null && level.overrideSettings && level.settings != null ? level.settings : this;
        }

        public ActionSound Copy()
        {
            var copy = (ActionSound)MemberwiseClone();
            copy.clips = clips == null ? Array.Empty<AudioClip>() : (AudioClip[])clips.Clone();
            copy.nextPlayAt = 0;
            copy.sizeSettings = Array.ConvertAll(sizeSettings ?? Array.Empty<SizeSettings>(), level =>
                level == null ? null : new SizeSettings
                {
                    overrideSettings = level.overrideSettings,
                    settings = level.settings?.CopySettings()
                });
            return copy;
        }
    }

    [Range(0, 1)] public float masterVolume = 1;
    public AudioMixerGroup output;
    [Range(0, 1)] public float spatialBlend;
    [Min(0.01f)] public float minDistance = 1;
    [Min(0.01f)] public float maxDistance = 40;
    [Range(1, 32)] public int maxVoices = 8;
    [Min(0.05f)] public float footstepInterval = 0.35f;
    [SerializeField] List<ActionSound> actions = new();

    float nextStepAt;
    public IReadOnlyList<ActionSound> Actions => actions;

    public List<Sfx> DetectActions()
    {
        var found = new List<Sfx>();
        foreach (var component in GetComponents<MonoBehaviour>())
        {
            if (component == null) continue;
            var declaration = Attribute.GetCustomAttribute(component.GetType(), typeof(SoundActionsAttribute)) as SoundActionsAttribute;
            if (declaration == null) continue;
            foreach (var action in declaration.actions)
                if (!found.Contains(action)) found.Add(action);
        }
        return found;
    }

    public bool RefreshActions()
    {
        bool changed = false;
        // Migrate the removed Collect action without renumbering serialized events.
        foreach (var legacy in actions.FindAll(x => (int)x.action == 23))
        {
            if (actions.Exists(x => x.action == Sfx.FoodPickup)) actions.Remove(legacy);
            else legacy.action = Sfx.FoodPickup;
            changed = true;
        }
        foreach (var action in DetectActions())
        {
            if (actions.Exists(x => x.action == action)) continue;
            // Preserve legacy impact clips and size overrides when upgrading an object.
            var legacy = action == Sfx.SwipeHitEnemy || action == Sfx.SwipeHitBuilding
                ? actions.Find(x => x.action == Sfx.SwipeHit) : null;
            var entry = legacy != null ? legacy.Copy() : new ActionSound();
            entry.action = action;
            actions.Add(entry);
            changed = true;
        }
        return changed;
    }

    public void AddManualAction()
    {
        foreach (Sfx id in Enum.GetValues(typeof(Sfx)))
            if (!actions.Exists(x => x.action == id)) { actions.Add(new ActionSound { action = id }); return; }
    }

    public void CopyFrom(SoundPlayer template)
    {
        masterVolume = template.masterVolume;
        output = template.output;
        spatialBlend = template.spatialBlend;
        minDistance = template.minDistance;
        maxDistance = template.maxDistance;
        maxVoices = template.maxVoices;
        footstepInterval = template.footstepInterval;
        actions = template.actions.ConvertAll(x => x.Copy());
        RefreshActions();
    }

    public static void Attach(GameObject owner, SoundPlayer template)
    {
        if (template == null) return;
        if (!owner.TryGetComponent<SoundPlayer>(out var player)) player = owner.AddComponent<SoundPlayer>();
        player.CopyFrom(template);
    }

    void Reset() => RefreshActions();
    void Awake() => RefreshActions();

    void Update()
    {
        var body = GetComponent<Rigidbody2D>();
        if (body == null || body.linearVelocity.sqrMagnitude < 0.04f) return;
        if (Time.time < nextStepAt) return;
        nextStepAt = Time.time + Mathf.Max(0.05f, footstepInterval);
        TryPlay(Sfx.Footstep);
    }

    // Suitable for UnityEvents and animation events.
    public void PlayAction(string action)
    {
        if (Enum.TryParse<Sfx>(action, out var id)) TryPlay(id);
    }

    public bool TryPlay(Sfx action, float gain = 1)
    {
        if (!Application.isPlaying || !isActiveAndEnabled) return false;
        var entry = actions.Find(x => x.action == action);
        if (entry == null) return false;
        // Read this object's current tier for every event, including grow/shrink.
        var progress = GetComponent<PlayerProgress>();
        var settings = entry.SettingsForSize(progress != null ? progress.SizeNumber : 0);
        if (settings == null || settings.clips == null) return false;
        var valid = Array.FindAll(settings.clips, x => x != null);
        if (valid.Length == 0) return false;
        if (!settings.enabled || Time.time < entry.nextPlayAt) return true;
        entry.nextPlayAt = Time.time + Mathf.Max(0, settings.cooldown);
        RuntimeSoundPlayer.Ensure().Play(this, settings,
            valid[UnityEngine.Random.Range(0, valid.Length)], transform.position, gain, action);
        return true;
    }
}
