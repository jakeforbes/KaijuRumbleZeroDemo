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
    Rect playfieldBounds;

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
        CameraRig.CancelActiveIntroduction();
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
        BuildingArt.ClearCache();
        DirectionalArt.ClearCache();
        Enemy.Reset();
        HitFx.Reset();
        Missile.Reset();
        UpgradePickup.Reset();
        Popups.Clear();
        ClearScene();
        var cam = BuildCamera();
        RuntimeSoundPlayer.Ensure();
        BackgroundMusicPlayer.Create(Resources.Load<MusicSettings>("Music Settings"));

        // Created before the city so buildings can register as they are made.
        fade = gameObject.AddComponent<OccluderFade>();
        fade.tuning = tuning;

        BuildGround();
        if (IsGym) BuildGym(); else BuildCity();
        player = BuildPlayer();
        if (IsGym) player.transform.position = GymPoint(-Mathf.Ceil(GymTiles() * 0.5f), -3);

        cam.GetComponent<CameraRig>().target = player.transform;
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);

        if (!IsGym) PlaceFreeUpgrades();
        gameObject.AddComponent<Popups>();

        var director = gameObject.AddComponent<WaveDirector>();
        director.tuning = tuning;
        director.player = player.transform;
        director.Running = !IsGym;

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
        go.AddComponent<AudioListener>();
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

        float spanX = IsGym ? GymWidth() + 12f : tuning.blocksX * tuning.blockSpacingX;
        float spanY = IsGym ? (GymTiles() + 12) * tileH * 0.5f : tuning.blocksY * tuning.blockSpacingY;
        // Rows sit half a tile apart so the diamonds interlock, so this needs
        // twice the count a full-tile spacing would.
        int cols = Mathf.CeilToInt(spanX / tileW) + 4;
        int rows = Mathf.CeilToInt(spanY / (tileH * 0.5f)) + 8;
        if (IsGym) { cols += cols % 2; rows += rows % 4 == 0 ? 0 : 4 - rows % 4; }
        playfieldBounds = Rect.MinMaxRect(-cols * 0.5f * tileW - tileW * 0.5f,
            -rows * 0.25f * tileH - tileH * 0.5f,
            (cols - cols * 0.5f) * tileW,
            (rows - 1 - rows * 0.5f) * tileH * 0.5f + tileH * 0.5f);

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

    // Both maps use this factory: art, collision, health, audio and drops stay identical.
    void CreateBuilding(Transform root, BuildingType type, int tilesX, int tilesY,
        int heightPx, Vector3 position, string objectName)
    {
        float ppu = tuning.pixelsPerUnit;
        var go = new GameObject(objectName);
        go.transform.SetParent(root, false);
        go.transform.position = position;

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
        {
            // Idealised tile math first, then the per-class calibration for delivered
            // art whose actual base doesn't line up with that math. Identity by
            // default, so greybox buildings (already exact) are untouched.
            Vector2 p = cornersPx[i] / ppu * tuning.buildingFootprint;
            p.x *= type.footprintScale.x;
            p.y *= type.footprintScale.y;
            points[i] = p + type.footprintOffset;
        }

        var col = go.AddComponent<PolygonCollider2D>();
        col.points = points;

        // Init last: it caches the collider and sprite renderer.
        building.Init(tuning, type, tilesX, tilesY, heightPx, ppu);
    }

    bool IsGym => SceneManager.GetActiveScene().name == "Gym";

    float GymTiles()
    {
        float length = 0;
        foreach (var type in tuning.buildingTypes)
            if (type != null) length += type.tilesX + 1;
        return length;
    }

    float GymWidth() => (GymTiles() + 6) * GreyboxArt.TileW * 0.5f / tuning.pixelsPerUnit;

    Vector3 GymPoint(float x, float y) => new Vector3(
        (x + y) * GreyboxArt.TileW * 0.5f / tuning.pixelsPerUnit,
        (y - x) * GreyboxArt.TileH * 0.5f / tuning.pixelsPerUnit, 0);

    void BuildGym()
    {
        var root = new GameObject("Gym Buildings").transform;
        float cursor = -Mathf.Ceil(GymTiles() * 0.5f);
        // Artwork first so the delivered models are immediately accessible.
        for (int pass = 0; pass < 2; pass++)
        foreach (var type in tuning.buildingTypes)
        {
            if (type == null || (!string.IsNullOrEmpty(type.artSprite) ? 0 : 1) != pass) continue;
            // Anchor the footprint corner to the ground lattice. Advance exactly
            // the footprint width plus one empty tile along the same grid axis.
            var position = GymPoint(cursor + type.tilesX * 0.5f, type.tilesY * 0.5f);
            CreateBuilding(root, type, type.tilesX, type.tilesY, type.minHeightPx,
                position, type.name);
            var label = new GameObject(type.name + " Label");
            label.transform.SetParent(root, false);
            label.transform.position = GymPoint(cursor + type.tilesX * 0.5f, -1);
            var text = label.AddComponent<TextMesh>();
            text.text = type.name;
            text.fontSize = 32;
            text.characterSize = 0.07f;
            text.anchor = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.GetComponent<MeshRenderer>().sortingOrder = 5;
            cursor += type.tilesX + 1;
        }
    }
    void BuildCity()
    {
        float ppu = tuning.pixelsPerUnit;
        var rng = new System.Random(tuning.randomSeed);

        var root = new GameObject("City").transform;
        float totalWeight = 0f;
        foreach (var t in tuning.buildingTypes) totalWeight += Mathf.Max(0f, t.weight);

        // Reactors are placed before anything else and spaced apart, rather than rolled
        // from the weight table. Random weights would happily put two side by side, and
        // two pulses in one screen is a very different thing from one.
        var reactorSlots = PickReactorSlots(rng);

        for (int by = 0; by < tuning.blocksY; by++)
            for (int bx = 0; bx < tuning.blocksX; bx++)
            {
                // Leave the centre clear so the player has room to start.
                if (Mathf.Abs(bx - tuning.blocksX / 2) <= 1 && Mathf.Abs(by - tuning.blocksY / 2) <= 1) continue;

                var type = reactorSlots.TryGetValue((bx, by), out var reactor)
                    ? reactor
                    : PickType(tuning.buildingTypes, totalWeight, rng);
                if (type == null) continue;

                // Flip non-square footprints so the grid does not read as one repeated shape.
                int tilesX = type.tilesX, tilesY = type.tilesY;
                if (type.AllowsFlip && rng.Next(0, 2) == 0) (tilesX, tilesY) = (tilesY, tilesX);

                int heightPx = rng.Next(type.minHeightPx, type.maxHeightPx + 1);

                float x = (bx - tuning.blocksX * 0.5f) * tuning.blockSpacingX;
                float y = (by - tuning.blocksY * 0.5f) * tuning.blockSpacingY;

                CreateBuilding(root, type, tilesX, tilesY, heightPx, new Vector3(x, y, 0f), $"{type.name}_{bx}_{by}");
            }
    }

    /// <summary>
    /// Scatters a few power-ups around the map at run start, free for the taking.
    /// Every other source is conditional — smash the right building, kill the elite —
    /// so early power depends on what the city happens to roll. These are guaranteed,
    /// evenly spaced, and reward exploring outward from the start.
    /// </summary>
    void PlaceFreeUpgrades()
    {
        var upgrades = PlayerUpgrades.Instance;
        if (upgrades == null || tuning.freeUpgradeCount <= 0) return;

        float ppu = tuning.pixelsPerUnit;

        for (int i = 0; i < tuning.freeUpgradeCount; i++)
        {
            // Even angles around the player's start, on the isometric ground ellipse.
            float angle = i / (float)tuning.freeUpgradeCount * Mathf.PI * 2f + Mathf.PI * 0.25f;

            for (int attempt = 0; attempt < 8; attempt++)
            {
                float r = tuning.freeUpgradeRadius - attempt * 1.5f;
                if (r < 4f) break;

                var at = new Vector3(Mathf.Cos(angle) * r,
                                     Mathf.Sin(angle) * r * tuning.isoSquash, 0f);
                if (Physics2D.OverlapCircle(at, 1f) != null) continue;

                UpgradePickup.Spawn(tuning, upgrades.RollDrop(), at, ppu);
                break;
            }
        }
    }

    public EnemyType FindType(string name)
    {
        if (tuning.enemyTypes == null) return null;
        foreach (var t in tuning.enemyTypes)
            if (t.name == name) return t;
        return null;
    }

    public bool TryFindBossSpawn(EnemyType type, out Vector3 position)
    {
        position = default;
        float clearance = Mathf.Max(0.6f, type.bodyPx * 0.5f / tuning.pixelsPerUnit);
        float inset = clearance + 0.5f;
        var edge = Rect.MinMaxRect(playfieldBounds.xMin + inset, playfieldBounds.yMin + inset,
            playfieldBounds.xMax - inset, playfieldBounds.yMax - inset);
        if (edge.width <= 0 || edge.height <= 0 || player == null) return false;
        // Search the perimeter rather than falling back to a ring around the player.
        // The farthest clear candidate leaves room for the approach and camera reveal.
        float best = -1;
        for (int side = 0; side < 4; side++)
            for (int sample = 0; sample <= 32; sample++)
            {
                float t = sample / 32f;
                Vector2 at = side == 0 ? new Vector2(Mathf.Lerp(edge.xMin, edge.xMax, t), edge.yMin)
                    : side == 1 ? new Vector2(edge.xMax, Mathf.Lerp(edge.yMin, edge.yMax, t))
                    : side == 2 ? new Vector2(Mathf.Lerp(edge.xMin, edge.xMax, t), edge.yMax)
                    : new Vector2(edge.xMin, Mathf.Lerp(edge.yMin, edge.yMax, t));
                if (Physics2D.OverlapCircle(at, clearance) != null) continue;
                var delta = at - (Vector2)player.transform.position;
                delta.y /= Mathf.Max(0.01f, tuning.isoSquash);
                float score = delta.sqrMagnitude;
                if (score <= best) continue;
                best = score;
                position = at;
            }
        return best >= 0;
    }

    /// <summary>Cheat spawn: one named enemy, for testing a type without waiting on the ratio.</summary>
    public void SpawnOne(string typeName)
    {
        var type = FindType(typeName);
        if (type == null || player == null)
        {
            Debug.LogWarning($"KRZ: no enemy type named '{typeName}'.");
            return;
        }

        if (type.isBoss || type.name == "Abomination")
        {
            // Enemy.Spawn centrally selects the edge, also covering other spawn callers.
            if (Enemy.Spawn(tuning, type, Vector3.zero, tuning.pixelsPerUnit) != null)
                Debug.Log($"KRZ: spawned {type.name} at the playfield edge.");
            return;
        }

        // Big bodies need room. Clear a radius matching the thing being spawned,
        // stepping outward until the ground is free, or the physics solver will
        // fling it out of the building it was born inside.
        float clearance = Mathf.Max(0.6f, type.bodyPx * 0.5f / tuning.pixelsPerUnit);
        float angle = Random.value * Mathf.PI * 2f;

        for (int attempt = 0; attempt < 8; attempt++)
        {
            float r = clearance + 4f + attempt * 1.5f;
            var at = player.transform.position +
                     new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * tuning.isoSquash, 0f);

            if (Physics2D.OverlapCircle(at, clearance) != null) continue;

            Enemy.Spawn(tuning, type, at, tuning.pixelsPerUnit);
            Debug.Log($"KRZ: spawned {type.name}.");
            return;
        }

        Debug.LogWarning($"KRZ: no clear ground for {type.name}. Move somewhere more open.");
    }

    /// <summary>
    /// Chooses which block slots become reactors, greedily accepting candidates that
    /// are far enough from every reactor already placed. Alternates the two sizes so
    /// a run always contains both.
    /// </summary>
    System.Collections.Generic.Dictionary<(int, int), BuildingType> PickReactorSlots(System.Random rng)
    {
        var chosen = new System.Collections.Generic.Dictionary<(int, int), BuildingType>();

        // Unique reactors are placed first and unconditionally. Left to compete for
        // slots with the common ones, a landmark that is supposed to exist once per
        // run could simply fail to appear.
        var uniques = new System.Collections.Generic.List<BuildingType>();
        var reactors = new System.Collections.Generic.List<BuildingType>();
        foreach (var t in tuning.buildingTypes)
        {
            if (!t.isReactor) continue;
            if (t.unique) uniques.Add(t); else reactors.Add(t);
        }
        if (uniques.Count == 0 && (reactors.Count == 0 || tuning.reactorCount <= 0)) return chosen;

        // Every slot outside the clear starting area, shuffled.
        var slots = new System.Collections.Generic.List<(int bx, int by)>();
        for (int by = 0; by < tuning.blocksY; by++)
            for (int bx = 0; bx < tuning.blocksX; bx++)
            {
                if (Mathf.Abs(bx - tuning.blocksX / 2) <= 1 && Mathf.Abs(by - tuning.blocksY / 2) <= 1) continue;
                slots.Add((bx, by));
            }

        for (int i = slots.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (slots[i], slots[j]) = (slots[j], slots[i]);
        }

        var placed = new System.Collections.Generic.List<Vector2>();
        int uniquesPlaced = 0;
        int commonPlaced = 0;

        foreach (var (bx, by) in slots)
        {
            bool wantUnique = uniquesPlaced < uniques.Count;
            if (!wantUnique && (reactors.Count == 0 || commonPlaced >= tuning.reactorCount)) break;

            var world = new Vector2((bx - tuning.blocksX * 0.5f) * tuning.blockSpacingX,
                                    (by - tuning.blocksY * 0.5f) * tuning.blockSpacingY);

            bool tooClose = false;
            foreach (var p in placed)
                if (Vector2.Distance(p, world) < tuning.reactorMinSpacing) { tooClose = true; break; }
            if (tooClose) continue;

            if (wantUnique) chosen[(bx, by)] = uniques[uniquesPlaced++];
            else chosen[(bx, by)] = reactors[commonPlaced++ % reactors.Count];

            placed.Add(world);
        }

        return chosen;
    }

    static BuildingType PickType(BuildingType[] types, float totalWeight, System.Random rng)
    {
        if (types == null || types.Length == 0 || totalWeight <= 0f) return null;

        float roll = (float)rng.NextDouble() * totalWeight;
        foreach (var t in types)
        {
            if (t.weight <= 0f) continue;
            roll -= t.weight;
            if (roll <= 0f) return t;
        }

        // Fall back to the last *weighted* type. Returning types[^1] would hand back a
        // zero-weight reactor on a floating-point edge, placing one outside the spacing
        // pass that exists to keep two off the same screen.
        for (int i = types.Length - 1; i >= 0; i--)
            if (types[i].weight > 0f) return types[i];
        return null;
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
        col.size = tuning.playerFootprintFraction;   // scaled up immediately by SetScale

        var pc = go.AddComponent<PlayerController>();
        pc.tuning = tuning;

        var progress = go.AddComponent<PlayerProgress>();
        progress.tuning = tuning;

        var upgrades = go.AddComponent<PlayerUpgrades>();
        upgrades.tuning = tuning;

        var attack = go.AddComponent<PlayerAttack>();
        attack.tuning = tuning;
        SoundPlayer.Attach(go, tuning.playerSounds);

        var special = go.AddComponent<PlayerSpecial>();
        special.tuning = tuning;

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

        // Real art takes over if the package is present; greybox stays otherwise, so
        // the build never depends on the art having been delivered.
        var uries = go.AddComponent<UriesArt>();
        uries.target = bsr;
        uries.player = pc;
        if (UriesArt.Available) bodyGo.transform.localScale = Vector3.one * UriesArt.CanvasScale;

        fade.playerArt = bsr;
        // OccluderFade compares ground pivots; render from that same point.
        bsr.spriteSortPoint = SpriteSortPoint.Pivot;
        progress.bodyArt = bsr;
        pc.BindArt(art);
        return pc;
    }

    /// <summary>Cheat spawn: a ring of enemies around the player, at the screen edge.</summary>
    public void SpawnSwarm(int count, int typeIndex = 0)
    {
        if (player == null)
        {
            Debug.LogWarning("KRZ: no player, cannot spawn.");
            return;
        }
        if (tuning.enemyTypes == null || tuning.enemyTypes.Length == 0)
        {
            Debug.LogWarning("KRZ: Tuning.enemyTypes is empty. Reset the Tuning asset or refill it.");
            return;
        }

        var type = tuning.enemyTypes[Mathf.Clamp(typeIndex, 0, tuning.enemyTypes.Length - 1)];
        var commander = FindType("Commander");
        bool rollCommanders = type != commander && commander != null;
        int spawned = 0;

        for (int i = 0; i < count; i++)
        {
            var spawning = type;
            if (rollCommanders)
            {
                int per = Random.Range(tuning.commanderPerMin, tuning.commanderPerMax + 1);
                if (per > 0 && Random.value < 1f / per) spawning = commander;
            }

            float angle = i / (float)count * Mathf.PI * 2f + Random.value;

            // Try a few radii outward. A dynamic body spawned inside a building gets
            // violently depenetrated and flung off, so find clear ground first.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                float r = 5.5f + attempt * 1.2f;
                var at = player.transform.position +
                         new Vector3(Mathf.Cos(angle) * r, Mathf.Sin(angle) * r * tuning.isoSquash, 0f);

                if (Physics2D.OverlapCircle(at, 0.5f) != null) continue;

                Enemy.Spawn(tuning, spawning, at, tuning.pixelsPerUnit);
                spawned++;
                break;
            }
        }

        Debug.Log($"KRZ: spawned {spawned}/{count} {type.name}. Total alive: {Enemy.All.Count}");
    }
}
