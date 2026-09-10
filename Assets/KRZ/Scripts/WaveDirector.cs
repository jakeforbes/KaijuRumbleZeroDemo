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
            // One-shot beats that were skipped over are written off rather than all
            // firing at once — except a boss, which still fires the moment the clock
            // passes it. Scrubbing forward is how anyone tests the late game, and
            // marking the boss spent on the way past meant the one thing you scrubbed
            // forward to see was the one thing that could never happen.
            if (!w.IsSustained && Clock >= w.startTime && !IsBossWave(w)) w.fired = true;
            if (w.nextFireAt < Clock) w.nextFireAt = Clock;
        }
    }

    static bool IsBossWave(WaveEntry wave)
    {
        var type = GameBootstrap.Instance != null
            ? GameBootstrap.Instance.FindType(wave.enemyType)
            : null;
        return type != null && (type.isBoss || type.name == "Abomination");
    }

    void Spawn(WaveEntry wave)
    {
        if (player == null) return;

        var type = GameBootstrap.Instance != null ? GameBootstrap.Instance.FindType(wave.enemyType) : null;
        if (type == null)
        {
            Debug.LogWarning($"KRZ: wave '{wave.label}' wants enemy type '{wave.enemyType}', which does not exist.");
            return;
        }

        // A boss places itself, and must not go through the squad path at all.
        //
        // Two gates in there were quietly eating it. The living-enemy cap is checked
        // per member, so at 2:40 with sixty things already on the map the boss wave
        // returned having spawned nothing. And the placement loop demands a clear
        // circle of the spawner's own radius — two and a half units for the
        // Abomination — which a city at double density fails six times out of six.
        //
        // Neither logged anything, and Enemy.Spawn discards the position it is handed
        // for a boss anyway and runs its own search. So the whole loop was a coin toss
        // the boss had to win in order to be allowed to place itself properly.
        if (IsBossWave(wave))
        {
            Debug.Log($"KRZ: wave '{wave.label}' spawning {type.name}.");
            if (Enemy.Spawn(tuning, type, player.position, tuning.pixelsPerUnit) == null)
                Debug.LogWarning($"KRZ: {type.name} found nowhere to spawn.");
            return;
        }

        var commander = GameBootstrap.Instance.FindUpgradeCarrier();

        // Just beyond the visible edge, so groups walk on rather than pop in.
        var cam = Camera.main;
        float offScreen = cam != null ? cam.orthographicSize * cam.aspect + 2.5f : 12f;

        float baseAngle = ShapeAngle(wave, offScreen);
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
                if (!OnLandPoint(at)) continue;
                if (Physics2D.OverlapCircle(at, clearance) != null) continue;

                Enemy.Spawn(tuning, spawning, at, tuning.pixelsPerUnit);
                break;
            }
        }
    }

    /// <summary>
    /// Whether a spawn just off screen in this direction lands on ground. Nothing may
    /// walk on water, so a wave sent seaward would simply never arrive — the player
    /// would be pinned against a coast wondering where the pressure went.
    /// </summary>
    bool OnLand(float angle, float radius)
    {
        if (GameBootstrap.Instance == null) return true;

        var land = GameBootstrap.Instance.LandBounds;
        if (land.width <= 0f) return true;

        // Inset by enough that a squad spawning shoulder to shoulder does not have
        // half its members standing in the surf.
        land = Rect.MinMaxRect(land.xMin + 3f, land.yMin + 3f, land.xMax - 3f, land.yMax - 3f);

        var at = player.position + new Vector3(Mathf.Cos(angle) * radius,
                                               Mathf.Sin(angle) * radius * tuning.isoSquash, 0f);
        return land.Contains(at);
    }

    /// <summary>The same test for one placed member, since stepping outward to find
    /// clear ground can walk the last of a squad off the coast.</summary>
    bool OnLandPoint(Vector3 at)
    {
        if (GameBootstrap.Instance == null) return true;

        var land = GameBootstrap.Instance.LandBounds;
        if (land.width <= 0f) return true;

        return Rect.MinMaxRect(land.xMin + 1.5f, land.yMin + 1.5f,
                               land.xMax - 1.5f, land.yMax - 1.5f).Contains(at);
    }

    /// <summary>Straight at the middle of the map, which is land by construction.</summary>
    float AngleToLand()
    {
        var centre = GameBootstrap.Instance != null ? GameBootstrap.Instance.LandBounds.center : Vector2.zero;
        Vector2 d = centre - (Vector2)player.position;
        if (d.sqrMagnitude < 0.001f) return Random.value * Mathf.PI * 2f;
        return Mathf.Atan2(d.y / tuning.isoSquash, d.x);
    }

    float ShapeAngle(WaveEntry wave, float offScreen)
    {
        if (wave.shape == SpawnShape.Ahead)
        {
            var pc = player.GetComponent<PlayerController>();
            if (pc != null && pc.AimDir.sqrMagnitude > 0.001f)
            {
                // Unsquash before taking the angle so "ahead" means ahead on the
                // ground, not ahead in a vertically compressed screen space.
                var flat = new Vector2(pc.AimDir.x, pc.AimDir.y / tuning.isoSquash);
                float ahead = Mathf.Atan2(flat.y, flat.x);
                if (OnLand(ahead, offScreen)) return ahead;

                // Running at the coast. Swing off the heading by as little as will
                // reach dry ground, so the group still lands broadly in your path
                // rather than jumping to the far side of you.
                for (int step = 1; step <= 8; step++)
                {
                    float turn = step * Mathf.PI / 8f;
                    if (OnLand(ahead + turn, offScreen)) return ahead + turn;
                    if (OnLand(ahead - turn, offScreen)) return ahead - turn;
                }
                return AngleToLand();
            }
        }

        // Successive clumps come from genuinely different sides. Pure random would
        // occasionally drop three groups on the same flank, which reads as one blob
        // rather than as being worked around.
        const float MinSeparation = 1.2f;   // radians, about 70 degrees
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            if (!OnLand(angle, offScreen)) continue;
            if (wave.lastAngle < -50f) return angle;

            float delta = Mathf.Abs(Mathf.DeltaAngle(angle * Mathf.Rad2Deg,
                                                     wave.lastAngle * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            if (delta >= MinSeparation) return angle;
        }

        // Backed into a corner with water on two sides, so separation gives way to
        // arriving at all. Inland is always a valid heading.
        for (int attempt = 0; attempt < 16; attempt++)
        {
            float angle = Random.value * Mathf.PI * 2f;
            if (OnLand(angle, offScreen)) return angle;
        }
        return AngleToLand();
    }
}
