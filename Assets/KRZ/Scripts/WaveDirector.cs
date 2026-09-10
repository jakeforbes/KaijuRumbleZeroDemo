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
        if (!Running || progress == null || progress.IsDead) return;

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
                w.nextFireAt = Clock + w.interval;
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

        var commander = GameBootstrap.Instance.FindType("Commander");

        // Just beyond the visible edge, so groups walk on rather than pop in.
        var cam = Camera.main;
        float offScreen = cam != null ? cam.orthographicSize * cam.aspect + 2.5f : 12f;

        float baseAngle = ShapeAngle(wave.shape);

        for (int i = 0; i < wave.count; i++)
        {
            // The global cap is what keeps a Vampire-Survivors-shaped timeline from
            // becoming a Vampire-Survivors-sized crowd. Checked per enemy, not per
            // wave, so a big group partially lands rather than being dropped whole.
            if (Enemy.All.Count >= tuning.maxEnemiesAlive) return;

            var spawning = type;
            if (wave.allowCommanders && commander != null && type != commander)
            {
                int per = Random.Range(tuning.commanderPerMin, tuning.commanderPerMax + 1);
                if (per > 0 && Random.value < 1f / per) spawning = commander;
            }

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

    float ShapeAngle(SpawnShape shape)
    {
        if (shape == SpawnShape.Ahead)
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
        return Random.value * Mathf.PI * 2f;
    }
}
