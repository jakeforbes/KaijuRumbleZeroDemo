using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Builds the whole prototype from code when Play is pressed, in any scene.
/// Nothing is hand-wired in the Editor, so nothing drifts and nothing needs
/// re-wiring after a change. Tuning values are read from the asset and never
/// written back, so tweaks survive a rebuild.
/// </summary>
public class GameBootstrap : MonoBehaviour
{
    public static GameBootstrap Instance { get; private set; }

    Tuning tuning;
    PlayerController player;
    OccluderFade fade;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (Instance != null) return;
        var go = new GameObject("~KRZ");
        Instance = go.AddComponent<GameBootstrap>();
    }

    /// <summary>
    /// Reloads the scene and rebuilds. Launch() only fires once per play session,
    /// so the rebuild has to be re-triggered explicitly after the scene comes back.
    /// </summary>
    public static void Restart()
    {
        Time.timeScale = 1f;
        Instance = null;
        SceneManager.sceneLoaded += OnReloaded;
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    static void OnReloaded(Scene scene, LoadSceneMode mode)
    {
        SceneManager.sceneLoaded -= OnReloaded;
        Launch();
    }

    void Awake()
    {
        Instance = this;
        tuning = Resources.Load<Tuning>("Tuning");
        if (tuning == null)
        {
            Debug.LogWarning("KRZ: no Tuning asset found, using defaults. " +
                             "It is created automatically once the Editor recompiles.");
            tuning = ScriptableObject.CreateInstance<Tuning>();
        }

        Food.Reset();
        Enemy.Reset();
        ClearScene();
        var cam = BuildCamera();

        // Created before the city so buildings can register as they are made.
        fade = gameObject.AddComponent<OccluderFade>();
        fade.tuning = tuning;

        BuildGround();
        BuildCity();
        player = BuildPlayer();

        cam.GetComponent<CameraRig>().target = player.transform;
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);

        var hud = gameObject.AddComponent<DebugHud>();
        hud.tuning = tuning;
        hud.player = player;
    }

    /// <summary>Removes whatever the sample scene shipped with so the build is deterministic.</summary>
    void ClearScene()
    {
        foreach (var c in FindObjectsByType<Camera>(FindObjectsSortMode.None)) Destroy(c.gameObject);
        foreach (var l in FindObjectsByType<Light>(FindObjectsSortMode.None)) Destroy(l.gameObject);
        Physics2D.gravity = Vector2.zero;
    }

    Camera BuildCamera()
    {
        var go = new GameObject("Camera");
        var cam = go.AddComponent<Camera>();
        cam.orthographic = true;
        cam.orthographicSize = tuning.baseOrthoSize;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.05f, 0.06f, 0.10f);
        cam.transform.position = new Vector3(0f, 0f, -10f);
        go.tag = "MainCamera";

        var rig = go.AddComponent<CameraRig>();
        rig.tuning = tuning;
        return cam;
    }

    void BuildGround()
    {
        float ppu = tuning.pixelsPerUnit;
        var a = GreyboxArt.GroundTile(new Color(0.13f, 0.15f, 0.21f), new Color(0.20f, 0.23f, 0.31f), ppu);
        var b = GreyboxArt.GroundTile(new Color(0.11f, 0.13f, 0.18f), new Color(0.20f, 0.23f, 0.31f), ppu);

        float tileW = GreyboxArt.TileW / ppu;   // 2 world units
        float tileH = GreyboxArt.TileH / ppu;   // 1 world unit

        float spanX = tuning.blocksX * tuning.blockSpacingX;
        float spanY = tuning.blocksY * tuning.blockSpacingY;
        // Rows sit half a tile apart so the diamonds interlock, so this needs
        // twice the count a full-tile spacing would.
        int cols = Mathf.CeilToInt(spanX / tileW) + 4;
        int rows = Mathf.CeilToInt(spanY / (tileH * 0.5f)) + 8;

        var root = new GameObject("Ground").transform;
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                // Offset every other row by half a tile so the diamonds interlock.
                float x = (c - cols * 0.5f) * tileW + (r % 2 == 0 ? 0f : tileW * 0.5f);
                float y = (r - rows * 0.5f) * tileH * 0.5f;

                var go = new GameObject("t");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = (r + c) % 2 == 0 ? a : b;
                sr.sortingOrder = -100;   // always behind everything that sorts by Y
            }
    }

    void BuildCity()
    {
        float ppu = tuning.pixelsPerUnit;
        var rng = new System.Random(tuning.randomSeed);

        var root = new GameObject("City").transform;
        float totalWeight = 0f;
        foreach (var t in tuning.buildingTypes) totalWeight += Mathf.Max(0f, t.weight);

        for (int by = 0; by < tuning.blocksY; by++)
            for (int bx = 0; bx < tuning.blocksX; bx++)
            {
                // Leave the centre clear so the player has room to start.
                if (Mathf.Abs(bx - tuning.blocksX / 2) <= 1 && Mathf.Abs(by - tuning.blocksY / 2) <= 1) continue;

                var type = PickType(tuning.buildingTypes, totalWeight, rng);
                if (type == null) continue;

                // Flip non-square footprints so the grid does not read as one repeated shape.
                int tilesX = type.tilesX, tilesY = type.tilesY;
                if (type.AllowsFlip && rng.Next(0, 2) == 0) (tilesX, tilesY) = (tilesY, tilesX);

                int heightPx = rng.Next(type.minHeightPx, type.maxHeightPx + 1);

                float x = (bx - tuning.blocksX * 0.5f) * tuning.blockSpacingX;
                float y = (by - tuning.blocksY * 0.5f) * tuning.blockSpacingY;

                var go = new GameObject($"{type.name}_{bx}_{by}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GreyboxArt.IsoBox(tilesX, tilesY, heightPx, type.colour, ppu);
                fade.Register(sr);

                var building = go.AddComponent<Building>();

                // Footprint only — never the sprite bounds, so the player can overlap
                // a tower's upper floors without colliding with them. Built from the
                // same corner function the sprite uses, so art and collision cannot
                // drift apart the way they did when this was an approximated capsule.
                var cornersPx = GreyboxArt.FootprintCornersPx(tilesX, tilesY);
                var points = new Vector2[cornersPx.Length];
                for (int i = 0; i < cornersPx.Length; i++)
                    points[i] = cornersPx[i] / ppu * tuning.buildingFootprint;

                var col = go.AddComponent<PolygonCollider2D>();
                col.points = points;

                // Init last: it caches the collider and sprite renderer.
                building.Init(tuning, type, tilesX, tilesY, heightPx, ppu);
            }
    }

    static BuildingType PickType(BuildingType[] types, float totalWeight, System.Random rng)
    {
        if (types == null || types.Length == 0 || totalWeight <= 0f) return null;

        float roll = (float)rng.NextDouble() * totalWeight;
        foreach (var t in types)
        {
            roll -= Mathf.Max(0f, t.weight);
            if (roll <= 0f) return t;
        }
        return types[types.Length - 1];
    }

    PlayerController BuildPlayer()
    {
        float ppu = tuning.pixelsPerUnit;

        var go = new GameObject("Player");
        go.transform.position = Vector3.zero;

        // Collider first: PlayerController.Awake runs the moment it is added and
        // caches the footprint, so it has to exist by then.
        var col = go.AddComponent<CapsuleCollider2D>();
        col.direction = CapsuleDirection2D.Horizontal;
        col.size = tuning.playerFootprint;

        var pc = go.AddComponent<PlayerController>();
        pc.tuning = tuning;

        var progress = go.AddComponent<PlayerProgress>();
        progress.tuning = tuning;

        var attack = go.AddComponent<PlayerAttack>();
        attack.tuning = tuning;

        // Art hangs off a child so growth scales the sprite without scaling the footprint.
        var art = new GameObject("Art").transform;
        art.SetParent(go.transform, false);

        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(art, false);
        var ssr = shadow.AddComponent<SpriteRenderer>();
        ssr.sprite = GreyboxArt.Shadow(160, ppu);
        ssr.sortingOrder = -1;

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(art, false);
        var bsr = bodyGo.AddComponent<SpriteRenderer>();
        bsr.sprite = GreyboxArt.Capsule(96, 128, new Color(0.55f, 0.85f, 0.45f), ppu);

        fade.playerArt = bsr;
        progress.bodyArt = bsr;
        pc.BindArt(art);
        return pc;
    }

    /// <summary>Cheat spawn: a ring of enemies around the player, just off screen.</summary>
    public void SpawnSwarm(int count, int typeIndex = 0)
    {
        if (player == null || tuning.enemyTypes == null || tuning.enemyTypes.Length == 0) return;

        var type = tuning.enemyTypes[Mathf.Clamp(typeIndex, 0, tuning.enemyTypes.Length - 1)];
        for (int i = 0; i < count; i++)
        {
            float angle = i / (float)count * Mathf.PI * 2f + Random.value;
            float r = Random.Range(7f, 10f);
            var offset = new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * tuning.isoSquash, 0f);
            Enemy.Spawn(tuning, type, player.transform.position + offset, tuning.pixelsPerUnit);
        }
    }
}
