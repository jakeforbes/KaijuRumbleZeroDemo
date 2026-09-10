using UnityEngine;

/// <summary>
/// Runs the three-minute arc. Everything about pacing lives on the Tuning asset as a
/// list of WaveEntry beats, so the whole shape of a run is tunable in the Inspector
/// while playing rather than compiled in.
///
/// Placement is deliberate. Enemies arrive as clumps on one side, or ahead of where
/// the player is heading, rather than sprinkled evenly around — at 1.28 speed a Grunt
/// cannot threaten by chasing, so the pressure has to come from arriving somewhere
/// inconvenient.
/// </summary>
public class WaveDirector : MonoBehaviour
{
    public static WaveDirector Instance { get; private set; }

    public Tuning tuning;
    public Transform player;

    /// <summary>Seconds since the run began. Cheats can move this.</summary>
    public float Clock { get; private set; }

    /// <summary>
    /// Breathing room after losing a size. Spawning thins out, but the clock keeps
    /// running — the boss still arrives on schedule, so recovering costs you progress
    /// toward the ending rather than delaying it. Losing a tier already hurts; being
    /// immediately buried again is what makes it stop being recoverable.
    /// </summary>
    float recoveryUntil = -1f;

    public bool Recovering => Time.time < recoveryUntil;
    public float RecoverySecondsLeft => Mathf.Max(0f, recoveryUntil - Time.time);

    public void BeginRecovery()
    {
        recoveryUntil = Time.time + tuning.recoverySeconds;

        // Push pending sustained beats out immediately, so relief is felt now rather
        // than after whatever was already queued lands on top of you.
        foreach (var w in tuning.waves)
            if (w.IsSustained && w.nextFireAt < Clock + w.interval)
                w.nextFireAt = Clock + w.interval * tuning.recoverySpawnInterval;
    }

    public string CurrentLabel { get; private set; } = "calm";
    public bool Running { get; set; } = true;

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Start()
    {
        foreach (var w in tuning.waves)
        {
            w.fired = false;
            w.nextFireAt = w.startTime;
        }
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (!Running || progress == null || progress.RunOver) return;

        Clock += Time.deltaTime;

        string active = null;
        foreach (var w in tuning.waves)
        {
            if (Clock < w.startTime) continue;

            if (w.IsSustained)
            {
                if (Clock > w.endTime) continue;
                active ??= w.label;
                if (Clock < w.nextFireAt) continue;
                w.nextFireAt = Clock + w.interval * (Recovering ? tuning.recoverySpawnInterval : 1f);
            }
            else
            {
                if (w.fired) continue;
                w.fired = true;
                active ??= w.label;
            }

            Spawn(w);
        }

        if (active != null) CurrentLabel = active;
    }

    /// <summary>Jumps the clock, firing nothing that was skipped. For testing the late game.</summary>
    public void Skip(float seconds)
    {
        Clock += seconds;
        foreach (var w in tuning.waves)
        {
            if (!w.IsSustained && Clock >= w.startTime) w.fired = true;
            if (w.nextFireAt < Clock) w.nextFireAt = Clock;
        }
    }

    void Spawn(WaveEntry wave)
    {
        if (player == null) return;

        var type = GameBootstrap.Instance != null ? GameBootstrap.Instance.FindType(wave.enemyType) : null;
        if (type == null) return;

        var commander = GameBootstrap.Instance.FindUpgradeCarrier();

        // Just beyond the visible edge, so groups walk on rather than pop in.
        var cam = Camera.main;
        float offScreen = cam != null ? cam.orthographicSize * cam.aspect + 2.5f : 12f;

        float baseAngle = ShapeAngle(wave);
        wave.lastAngle = baseAngle;

        // One Commander leads the group when it is this beat's turn. Placed at a
        // random index so it is not always the same member of the squad.
        bool wantsCommander = wave.IsCommanderTurn && commander != null && type != commander;
        int commanderIndex = wantsCommander ? Random.Range(0, wave.count) : -1;

        // The veteran variant takes over the line gradually, so the step from light
        // infantry to armour is a slope rather than a cliff.
        var veteran = string.IsNullOrEmpty(wave.veteranType)
            ? null
            : GameBootstrap.Instance.FindType(wave.veteranType);

        float veteranShare = 0f;
        if (veteran != null && wave.fireIndex >= wave.veteranFromFire)
            veteranShare = Mathf.Clamp01(wave.veteranStartFraction +
                                         (wave.fireIndex - wave.veteranFromFire) * wave.veteranRampPerFire);

        wave.fireIndex++;

        for (int i = 0; i < wave.count; i++)
        {
            // The global cap is what keeps a Vampire-Survivors-shaped timeline from
            // becoming a Vampire-Survivors-sized crowd. Checked per enemy, not per
            // wave, so a big group partially lands rather than being dropped whole.
            if (Enemy.All.Count >= tuning.maxEnemiesAlive) return;

            var spawning = i == commanderIndex ? commander
                         : veteranShare > 0f && Random.value < veteranShare ? veteran
                         : type;

            float angle = wave.shape == SpawnShape.Ring
                ? baseAngle + i / (float)wave.count * Mathf.PI * 2f
                : baseAngle + Random.Range(-0.45f, 0.45f);

            float clearance = Mathf.Max(0.6f, spawning.bodyPx * 0.5f / tuning.pixelsPerUnit);
            float spread = wave.shape == SpawnShape.Ring ? 0f : Random.Range(0f, 2.5f);

            for (int attempt = 0; attempt < 6; attempt++)
            {
                float r = offScreen + spread + attempt * 1.5f;
                var at = player.position + new Vector3(Mathf.Cos(angle) * r,
                                                       Mathf.Sin(angle) * r * tuning.isoSquash, 0f);
                if (Physics2D.OverlapCircle(at, clearance) != null) continue;

                Enemy.Spawn(tuning, spawning, at, tuning.pixelsPerUnit);
                break;
            }
        }
    }

    float ShapeAngle(WaveEntry wave)
    {
        if (wave.shape == SpawnShape.Ahead)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null && pc.AimDir.sqrMagnitude > 0.001f)
            {
                // Unsquash before taking the angle so "ahead" means ahead on the
                // ground, not ahead in a vertically compressed screen space.
                var flat = new Vector2(pc.AimDir.x, pc.AimDir.y / tuning.isoSquash);
                return Mathf.Atan2(flat.y, flat.x);
            }
        }

        // Successive clumps come from genuinely different sides. Pure random would
        // occasionally drop three groups on the same flank, which reads as one blob
        // rather than as being worked around.
        const float MinSeparation = 1.2f;   // radians, about 70 degrees
        for (int attempt = 0; attempt < 8; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            if (wave.lastAngle < -50f) return angle;

            float delta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg,
                                                     wave.lastAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            if (delta >= MinSeparation) return angle;
        }
        return Random.value * Mathf.PI * 2f;
    }
}
