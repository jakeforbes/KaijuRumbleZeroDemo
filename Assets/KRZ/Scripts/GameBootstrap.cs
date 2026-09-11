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
    Rect landBounds;
    Sprite[] waterFrames;
    Sprite[] groundTiles;

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
        SwarmBolt.Reset();
        ToxicField.Reset();
        UpgradePickup.Reset();
        Hamburger.Reset();
        Popups.Clear();
        ClearScene();
        var cam = BuildCamera();
        RuntimeSoundPlayer.Ensure();
        BackgroundMusicPlayer.Create(Resources.Load<MusicSettings>("Music Settings"));

        // Created before the city so buildings can register as they are made.
        fade = gameObject.AddComponent<OccluderFade>();
        fade.tuning = tuning;

        BuildGround();
        ReportBuildingArt();
        if (IsGym) BuildGym(); else BuildCity();
        player = BuildPlayer();
        if (IsGym) player.transform.position = GymPoint(-Mathf.Ceil(GymTiles() * 0.5f), -3);

        cam.GetComponent<CameraRig>().target = player.transform;
        cam.transform.position = new Vector3(player.transform.position.x, player.transform.position.y, -10f);

        if (!IsGym) { PlaceFreeUpgrades(); PlaceHamburgers(); }
        gameObject.AddComponent<Popups>();

        var director = gameObject.AddComponent<WaveDirector>();
        director.tuning = tuning;
        director.player = player.transform;
        director.Running = !IsGym;

        gameObject.AddComponent<BossObjectiveUi>();
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

    /// <summary>
    /// Ground the player and every enemy can stand on. Everything outside it is water.
    /// Derived from where the outermost blocks actually sit rather than assumed to be
    /// centred, because an even block count puts the city half a block off origin.
    /// </summary>
    public Rect LandBounds => landBounds;

    Rect CityLandBounds()
    {
        float minX = (0 - tuning.blocksX * 0.5f) * tuning.blockSpacingX;
        float maxX = (tuning.blocksX - 1 - tuning.blocksX * 0.5f) * tuning.blockSpacingX;
        float minY = (0 - tuning.blocksY * 0.5f) * tuning.blockSpacingY;
        float maxY = (tuning.blocksY - 1 - tuning.blocksY * 0.5f) * tuning.blockSpacingY;

        float m = tuning.shoreMargin;
        return Rect.MinMaxRect(minX - m, minY - m, maxX + m, maxY + m);
    }

    void BuildGround()
    {
        float ppu = tuning.pixelsPerUnit;
        var a = GreyboxArt.GroundTile(new Color(0.13f, 0.15f, 0.21f), new Color(0.20f, 0.23f, 0.31f), ppu);
        var b = GreyboxArt.GroundTile(new Color(0.11f, 0.13f, 0.18f), new Color(0.20f, 0.23f, 0.31f), ppu);
        var shallowA = GreyboxArt.GroundTile(new Color(0.10f, 0.24f, 0.36f), new Color(0.16f, 0.36f, 0.50f), ppu);
        var shallowB = GreyboxArt.GroundTile(new Color(0.08f, 0.20f, 0.31f), new Color(0.16f, 0.36f, 0.50f), ppu);

        float tileW = GreyboxArt.TileW / ppu;   // 2 world units
        float tileH = GreyboxArt.TileH / ppu;   // 1 world unit

        if (IsGym)
        {
            float gymSpanX = GymWidth() + 12f;
            float gymSpanY = (GymTiles() + 12) * tileH * 0.5f;
            int gymCols = Mathf.CeilToInt(gymSpanX / tileW) + 4;
            int gymRows = Mathf.CeilToInt(gymSpanY / (tileH * 0.5f)) + 8;
            gymCols += gymCols % 2;
            gymRows += gymRows % 4 == 0 ? 0 : 4 - gymRows % 4;

            playfieldBounds = Rect.MinMaxRect(-gymCols * 0.5f * tileW - tileW * 0.5f,
                -gymRows * 0.25f * tileH - tileH * 0.5f,
                (gymCols - gymCols * 0.5f) * tileW,
                (gymRows - 1 - gymRows * 0.5f) * tileH * 0.5f + tileH * 0.5f);
            landBounds = playfieldBounds;

            LayGround(new GameObject("Ground").transform, Vector2.zero, gymCols, gymRows,
                      tileW, tileH, a, b, null, null);
            return;
        }

        landBounds = CityLandBounds();
        playfieldBounds = landBounds;

        BuildOcean();

        // The lattice carries a little way into the water so the shoreline reads as
        // the same ground rather than as a rectangle cut out of a flat colour.
        float band = tuning.shallowBand;
        float spanX = landBounds.width + band * 2f;
        float spanY = landBounds.height + band * 2f;

        // Rows sit half a tile apart so the diamonds interlock, so this needs
        // twice the count a full-tile spacing would.
        int cols = Mathf.CeilToInt(spanX / tileW) + 4;
        int rows = Mathf.CeilToInt(spanY / (tileH * 0.5f)) + 8;

        var root = new GameObject("Ground").transform;

        // Delivered slab tiles, laid on the same lattice the greybox used. They are
        // opaque and overlap slightly, so nothing shows through and there is no quad
        // underneath to pay for.
        if (tuning.useGroundArt) groundTiles = LoadGroundTiles();

        if (!tuning.drawStreets && (groundTiles == null || groundTiles.Length == 0))
        {
            // Flat ground is one quad. With no seams, no markings and no kerbs there
            // is nothing for a lattice to express, and drawing thirteen thousand
            // identical diamonds to render one colour is thirteen thousand objects
            // spent on nothing. Only the coast keeps its tiles.
            var landGo = new GameObject("Land");
            landGo.transform.SetParent(root, false);
            landGo.transform.position = landBounds.center;

            var lsr = landGo.AddComponent<SpriteRenderer>();
            lsr.sprite = CityGroundArt.Concrete(tuning.groundTilePx, tuning.groundChunkPx,
                                                tuning.groundGrain, tuning.groundPatch, ppu);
            lsr.color = tuning.groundColour;

            // Between the ocean at -110 and the coastal lattice at -100, so the
            // half-transparent shore tiles overlap the land edge and soften it
            // instead of fighting the quad for the same sorting slot.
            lsr.sortingOrder = -101;

            // Tiled rather than stretched, so the grain stays at its authored pixel
            // size across a hundred and twenty units of ground instead of smearing.
            lsr.drawMode = SpriteDrawMode.Tiled;
            lsr.tileMode = SpriteTileMode.Continuous;
            lsr.size = new Vector2(landBounds.width, landBounds.height);
        }

        // With slab art the whole land is tiled; without it, only the coast is, and
        // the flat quad above covers the rest.
        bool coastOnly = !tuning.drawStreets && (groundTiles == null || groundTiles.Length == 0);

        LayGround(root, landBounds.center, cols, rows,
                  tileW, tileH, null, null, shallowA, shallowB, coastOnly);
    }

    /// <summary>
    /// The delivered slab tiles, sized so each one's top surface covers a lattice cell.
    ///
    /// A single pixels-per-unit for all twelve, taken from the narrowest tile rather
    /// than the average: at that size every tile covers at least a full cell and the
    /// wider ones overlap by about a tenth of a unit. Overlap between opaque tiles is
    /// invisible — they sort by ground position like everything else, so the nearer
    /// one simply wins — whereas a gap is a seam, and this is a floor.
    /// </summary>
    Sprite[] LoadGroundTiles()
    {
        var found = new System.Collections.Generic.List<Sprite>();
        var pivot = new Vector2(0.5f, tuning.groundTilePivotY);

        for (int i = 1; i <= 12; i++)
        {
            var tex = Resources.Load<Texture2D>($"Ground/ground_tile_{i:00}");
            if (tex == null) continue;

            var s = Sprite.Create(tex, new Rect(0f, 0f, tex.width, tex.height), pivot,
                                  Mathf.Max(1f, tuning.groundTilePpu), 0, SpriteMeshType.FullRect);
            s.name = $"ground_tile_{i:00}";
            found.Add(s);
        }

        if (found.Count == 0) Debug.LogWarning("KRZ: no ground tiles found, using the flat quad.");
        return found.ToArray();
    }

    /// <summary>
    /// Lays the interlocking diamond lattice. When shallow sprites are supplied, tiles
    /// whose centre falls outside the land are drawn as water instead of skipped, so
    /// the coast keeps the lattice instead of ending on a straight edge.
    ///
    /// Passing a null land sprite switches the floor to the generated city surface —
    /// roads on the block boundaries, lots inside them. The Gym keeps the flat
    /// checker, because a street plan drawn under a row of specimens is noise.
    /// </summary>
    void LayGround(Transform root, Vector2 centre, int cols, int rows, float tileW, float tileH,
                   Sprite a, Sprite b, Sprite shallowA, Sprite shallowB, bool waterOnly = false)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < cols; c++)
            {
                // Offset every other row by half a tile so the diamonds interlock.
                float x = centre.x + (c - cols * 0.5f) * tileW + (r % 2 == 0 ? 0f : tileW * 0.5f);
                float y = centre.y + (r - rows * 0.5f) * tileH * 0.5f;

                bool even = (r + c) % 2 == 0;
                bool water = shallowA != null && !landBounds.Contains(new Vector2(x, y));

                // Flat ground is already covered by a single quad, so the lattice is
                // only laid where it is doing something: softening the coastline.
                if (waterOnly && !water) continue;

                Sprite sprite;
                if (water) sprite = even ? shallowA : shallowB;
                else if (a != null) sprite = even ? a : b;
                else if (groundTiles != null && groundTiles.Length > 0)
                {
                    // Hashed off the cell rather than rolled, so the floor is identical
                    // every run and a tile never lands beside a copy of itself by luck
                    // of the draw ordering.
                    int h = Mathf.Abs(c * 73856093 ^ r * 19349663);
                    sprite = groundTiles[h % groundTiles.Length];
                }
                else sprite = CityTile(x, y);

                var go = new GameObject("t");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, y, 0f);
                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = sprite;
                sr.sortingOrder = -100;   // always behind everything that sorts by Y

                // Shallows are the same lattice let half way through to the moving
                // water underneath, so the coast is where the ripple starts showing
                // rather than a ring of flat blue diamonds sitting on top of it.
                if (water) sr.color = new Color(1f, 1f, 1f, tuning.shallowOpacity);
                else if (groundTiles != null && groundTiles.Length > 0) sr.color = tuning.groundArtTint;
            }
    }

    /// <summary>
    /// Picks a floor tile for a world position: road where it straddles a street
    /// between blocks, kerb just off one, lot everywhere else.
    ///
    /// Tested in world units against the block gridlines rather than in tile indices,
    /// because block spacing is 7 by 3.5 and tiles are 2 by 1 — the two grids do not
    /// divide, and forcing them to would mean changing the city's spacing to suit its
    /// paving. The road sprite is symmetrical, so being half a tile out of phase with
    /// the street it marks does not show.
    /// </summary>
    Sprite CityTile(float x, float y)
    {
        // Streets run down the middle between block centres, hence the half offset.
        float gx = Mathf.Abs(Mathf.Repeat(x / tuning.blockSpacingX + 0.5f, 1f) - 0.5f) * tuning.blockSpacingX;
        float gy = Mathf.Abs(Mathf.Repeat(y / tuning.blockSpacingY + 0.5f, 1f) - 0.5f) * tuning.blockSpacingY;

        float half = tuning.roadWidth * 0.5f;
        bool onX = gx <= half;     // a street running north to south
        bool onY = gy <= half;     // a street running east to west

        if (onX && onY) return Floor(CityGroundArt.Kind.Crossing, 0);
        if (onX) return Floor(CityGroundArt.Kind.RoadV, 0);
        if (onY) return Floor(CityGroundArt.Kind.RoadU, 0);

        if (gx <= half + tuning.kerbWidth || gy <= half + tuning.kerbWidth)
            return Floor(CityGroundArt.Kind.Kerb, 0);

        // Lot variety is hashed off the position, so it is stable across a rebuild
        // and does not need the seeded generator threaded down here.
        int hash = Mathf.Abs(Mathf.RoundToInt(x * 7.3f) * 73856093 ^ Mathf.RoundToInt(y * 11.7f) * 19349663);
        return Floor(CityGroundArt.Kind.Lot, hash % 4);
    }

    Sprite Floor(CityGroundArt.Kind kind, int variant)
    {
        int key = (int)kind * 8 + variant;
        if (floorTiles.TryGetValue(key, out var s)) return s;

        s = CityGroundArt.Tile(kind, variant, tuning.pixelsPerUnit);
        floorTiles[key] = s;
        return s;
    }

    readonly System.Collections.Generic.Dictionary<int, Sprite> floorTiles = new();

    /// <summary>
    /// Deep water, and the walls that make it impassable.
    ///
    /// One flat quad rather than more lattice: the ocean only has to reach past the
    /// screen edge, and tiling forty units of it in every direction would cost more
    /// objects than the entire city does.
    ///
    /// The walls are the one place in this project where an invisible barrier is the
    /// right answer, because it is not invisible — the water is the barrier, and the
    /// collider sits exactly on the waterline the player can see.
    /// </summary>
    void BuildOcean()
    {
        float ppu = tuning.pixelsPerUnit;
        float w = tuning.oceanWidth;

        var seaGo = new GameObject("Ocean");
        seaGo.transform.position = landBounds.center;

        waterFrames ??= WaterArt.Build(tuning.waterFrames, tuning.waterTilePx,
                                       tuning.waterCells, tuning.waterChunkPx, ppu);

        // One tiled renderer, sized to the whole sea. Under the ground lattice at -100.
        WaterSurface.Attach(seaGo, waterFrames,
                            new Vector2(landBounds.width + w * 2f, landBounds.height + w * 2f),
                            tuning.deepWaterTint, -110, tuning.waterFps);

        var walls = new GameObject("Shore").transform;
        walls.position = Vector3.zero;

        // Thick, so nothing crossing at knockback speed can pass through in one step.
        const float Thickness = 20f;
        AddWall(walls, new Vector2(landBounds.center.x, landBounds.yMin - Thickness * 0.5f),
                new Vector2(landBounds.width + Thickness * 2f, Thickness));
        AddWall(walls, new Vector2(landBounds.center.x, landBounds.yMax + Thickness * 0.5f),
                new Vector2(landBounds.width + Thickness * 2f, Thickness));
        AddWall(walls, new Vector2(landBounds.xMin - Thickness * 0.5f, landBounds.center.y),
                new Vector2(Thickness, landBounds.height + Thickness * 2f));
        AddWall(walls, new Vector2(landBounds.xMax + Thickness * 0.5f, landBounds.center.y),
                new Vector2(Thickness, landBounds.height + Thickness * 2f));
    }

    static void AddWall(Transform parent, Vector2 centre, Vector2 size)
    {
        var go = new GameObject("shore");
        go.transform.SetParent(parent, false);
        go.transform.position = centre;
        go.AddComponent<BoxCollider2D>().size = size;
    }

    // Both maps use this factory: art, collision, health, audio and drops stay identical.
    void CreateBuilding(Transform root, BuildingType type, int tilesX, int tilesY,
        int heightPx, Vector3 position, string objectName, bool flipped = false, int direction = 0)
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
        building.Init(tuning, type, tilesX, tilesY, heightPx, ppu, flipped, direction);
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
    /// <summary>
    /// Says which building types found their art and which fell back to greybox.
    ///
    /// Worth the console line: "the art isn't appearing" is otherwise indistinguishable
    /// from "the art is appearing on two of ten types", and we have now spent two
    /// rounds on that exact ambiguity. A missing file is silent by design everywhere
    /// else, which is right for shipping and useless for diagnosing.
    /// </summary>
    void ReportBuildingArt()
    {
        var missing = new System.Collections.Generic.List<string>();
        int found = 0;

        foreach (var t in tuning.buildingTypes)
        {
            if (t == null) continue;
            var probe = BuildingArt.LoadStages(t, 0, tuning.pixelsPerUnit);
            if (probe[BuildingArt.Pristine] != null) found++;
            else missing.Add(string.IsNullOrEmpty(t.artSprite) ? $"{t.name} (no path set)" : t.artSprite);
        }

        if (missing.Count == 0) Debug.Log($"KRZ: building art found for all {found} types.");
        else Debug.LogWarning($"KRZ: building art found for {found} type(s), greybox for " +
                              $"{missing.Count}: {string.Join(", ", missing)}");
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

        // Every footprint placed so far, so infill can find real gaps.
        var placed = new System.Collections.Generic.List<Rect>();
        var open = new System.Collections.Generic.List<(int bx, int by, float x, float y)>();

        for (int by = 0; by < tuning.blocksY; by++)
            for (int bx = 0; bx < tuning.blocksX; bx++)
            {
                // Only the block the player is standing on is left out, so the run
                // opens surrounded rather than in a clearing. A plaza at the start
                // meant the first thing you did was walk somewhere; food is the whole
                // economy, and it should be within reach of the spawn point.
                if (Mathf.Abs(bx - tuning.blocksX / 2) <= tuning.startClearBlocks &&
                    Mathf.Abs(by - tuning.blocksY / 2) <= tuning.startClearBlocks) continue;

                int district = DistrictTarget(bx, by);
                var type = reactorSlots.TryGetValue((bx, by), out var reactor)
                    ? reactor
                    : PickType(tuning.buildingTypes, rng, district);
                if (type == null) continue;

                // Flip non-square footprints so the grid does not read as one repeated shape.
                int tilesX = type.tilesX, tilesY = type.tilesY;
                bool flipped = type.AllowsFlip && rng.Next(0, 2) == 0;
                if (flipped) (tilesX, tilesY) = (tilesY, tilesX);

                int heightPx = rng.Next(type.minHeightPx, type.maxHeightPx + 1);

                float x = (bx - tuning.blocksX * 0.5f) * tuning.blockSpacingX;
                float y = (by - tuning.blocksY * 0.5f) * tuning.blockSpacingY;

                // Break the lattice. On exact rails the eye finds the grid instantly
                // and the city reads as a spreadsheet; a couple of units of wander is
                // enough to lose it without losing the block structure underneath.
                // Vertical wander is halved to match the 2:1 projection, or the same
                // number would read as twice as much drift going up the screen.
                //
                // Rolled every time so the sequence does not shift when a candidate is
                // rejected — a given randomSeed has to build the same city.
                float jx = ((float)rng.NextDouble() - 0.5f) * 2f * tuning.blockJitter;
                float jy = ((float)rng.NextDouble() - 0.5f) * tuning.blockJitter;

                // Take the wander only if the base still clears its neighbours. Block
                // spacing on the short axis is 3.5 and a 3x3's diamond is 3 tall, so
                // an unchecked nudge can bury one base under the next — which is what
                // reads as a building being clipped along its bottom edge.
                if (!Overlaps(placed, new Vector3(x + jx, y + jy, 0f), tilesX, tilesY, tuning.infillGap))
                {
                    x += jx;
                    y += jy;
                }

                // Rolled from the same seeded generator as everything else, so a given
                // randomSeed always builds the identical city.
                int direction = rng.Next(0, Mathf.Clamp(type.artDirections, 1, BuildingArt.DirectionCount));

                CreateBuilding(root, type, tilesX, tilesY, heightPx, new Vector3(x, y, 0f),
                               $"{type.name}_{bx}_{by}", flipped, direction);

                Occupy(placed, new Vector3(x, y, 0f), tilesX, tilesY);
                open.Add((bx, by, x, y));
            }

        // Second pass, because a 3x3 in the next block reaches three units toward this
        // one and infill placed before it existed would end up inside it. Every main
        // footprint has to be on the list before any gap is judged to be a gap.
        foreach (var (bx, by, x, y) in open) Infill(root, placed, rng, bx, by, x, y);
    }

    /// <summary>
    /// The screen-space box a footprint covers, in world units. A w by h diamond is
    /// (w+h) across and half that tall, which falls straight out of the tile maths.
    /// </summary>
    static Rect Footprint(Vector3 at, int tilesX, int tilesY)
    {
        float w = tilesX + tilesY;
        return new Rect(at.x - w * 0.5f, at.y - w * 0.25f, w, w * 0.5f);
    }

    static void Occupy(System.Collections.Generic.List<Rect> placed, Vector3 at, int tilesX, int tilesY)
        => placed.Add(Footprint(at, tilesX, tilesY));

    /// <summary>Whether a footprint here would sit inside another, given a clearance.</summary>
    static bool Overlaps(System.Collections.Generic.List<Rect> placed, Vector3 at,
                         int tilesX, int tilesY, float gap)
    {
        var box = Footprint(at, tilesX, tilesY);
        box = new Rect(box.x - gap, box.y - gap * 0.5f,
                       box.width + gap * 2f, box.height + gap);

        foreach (var r in placed)
            if (r.Overlaps(box)) return true;

        return false;
    }

    /// <summary>
    /// Packs small buildings into whatever the block's main building left over.
    ///
    /// Doubling the map doubled the walking without adding anything to walk past, and
    /// one building per block leaves most of a block empty at these spacings. This is
    /// the fill: small types only, placed where they actually fit.
    ///
    /// Overlap is tested against a list rather than Physics2D, because colliders made
    /// this same frame are not queryable until the physics system syncs, and a silent
    /// miss here would spawn buildings inside each other.
    /// </summary>
    void Infill(Transform root, System.Collections.Generic.List<Rect> placed,
                System.Random rng, int bx, int by, float blockX, float blockY)
    {
        int wanted = tuning.infillPerBlock;
        if (wanted <= 0) return;

        // Room to breathe around the spawn point, whatever the block roll said.
        if (Mathf.Abs(bx - tuning.blocksX / 2) <= tuning.startClearBlocks &&
            Mathf.Abs(by - tuning.blocksY / 2) <= tuning.startClearBlocks) return;

        for (int i = 0; i < wanted; i++)
        {
            var type = PickInfillType(rng);
            if (type == null) return;

            int tilesX = type.tilesX, tilesY = type.tilesY;
            bool flipped = type.AllowsFlip && rng.Next(0, 2) == 0;
            if (flipped) (tilesX, tilesY) = (tilesY, tilesX);

            for (int attempt = 0; attempt < 8; attempt++)
            {
                // Confined to the block's interior rather than its whole cell, so the
                // corridors on the block boundaries survive however dense it gets.
                float ox = ((float)rng.NextDouble() - 0.5f) * tuning.blockSpacingX * tuning.infillSpread;
                float oy = ((float)rng.NextDouble() - 0.5f) * tuning.blockSpacingY * tuning.infillSpread;
                var at = new Vector3(blockX + ox, blockY + oy, 0f);

                if (Overlaps(placed, at, tilesX, tilesY, tuning.infillGap)) continue;

                int heightPx = rng.Next(type.minHeightPx, type.maxHeightPx + 1);
                int direction = rng.Next(0, Mathf.Clamp(type.artDirections, 1, BuildingArt.DirectionCount));

                CreateBuilding(root, type, tilesX, tilesY, heightPx, at,
                               $"{type.name}_{bx}_{by}_fill{i}", flipped, direction);
                Occupy(placed, at, tilesX, tilesY);
                break;
            }
        }
    }

    /// <summary>Small civilian types only. Filler is filler, not a second skyline.</summary>
    BuildingType PickInfillType(System.Random rng)
    {
        float total = 0f;
        foreach (var t in tuning.buildingTypes)
            if (t.weight > 0f && t.sizeClass <= tuning.infillMaxClass) total += t.weight;
        if (total <= 0f) return null;

        float roll = (float)rng.NextDouble() * total;
        foreach (var t in tuning.buildingTypes)
        {
            if (t.weight <= 0f || t.sizeClass > tuning.infillMaxClass) continue;
            roll -= t.weight;
            if (roll <= 0f) return t;
        }
        return null;
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

    /// <summary>
    /// One hamburger per district quadrant, out where the quadrant lives rather than
    /// near the start. Going to get one should mean leaving downtown — that trip is
    /// the decision, and a pull radius is only worth having if you had to travel for it.
    /// </summary>
    void PlaceHamburgers()
    {
        if (tuning.hamburgerCount <= 0) return;

        float ppu = tuning.pixelsPerUnit;
        float radius = Mathf.Max(landBounds.width, landBounds.height) * tuning.hamburgerRadiusFraction;

        // Colliders built this frame are not queryable until the physics system has
        // caught up with their transforms, and a miss here buries a hamburger inside
        // a tower where nobody will ever reach it.
        Physics2D.SyncTransforms();

        var centre = landBounds.center;
        var quadrants = new[]
        {
            new Vector2( 0.5f,  0.5f), new Vector2(-0.5f,  0.5f),
            new Vector2(-0.5f, -0.5f), new Vector2( 0.5f, -0.5f),
        };

        for (int i = 0; i < tuning.hamburgerCount; i++)
        {
            var q = quadrants[i % quadrants.Length];
            var ideal = centre + new Vector2(q.x * landBounds.width * 0.5f,
                                             q.y * landBounds.height * 0.5f);

            // Spiral outward from the quadrant's heart until the ground is free.
            bool placed = false;
            for (int attempt = 0; attempt < 60 && !placed; attempt++)
            {
                float angle = attempt * 2.4f;
                float r = attempt * 0.6f;
                var at = new Vector3(ideal.x + Mathf.Cos(angle) * r,
                                     ideal.y + Mathf.Sin(angle) * r * tuning.isoSquash, 0f);

                if (!landBounds.Contains(at)) continue;
                if (Physics2D.OverlapCircle(at, 1.4f) != null) continue;

                Hamburger.Spawn(tuning, at, radius, ppu);
                placed = true;
            }

            if (!placed) Debug.LogWarning($"KRZ: no clear ground for hamburger {i}.");
        }
    }

    public EnemyType FindType(string name)
    {
        if (tuning.enemyTypes == null) return null;
        foreach (var t in tuning.enemyTypes)
            if (t.name == name) return t;
        return null;
    }

    /// <summary>
    /// Whichever enemy is currently the one worth stopping for. The Commander early,
    /// the Elite Tank once the kaiju is big enough that a Commander dies in passing —
    /// a prize you collect by walking over it has stopped being a decision.
    ///
    /// One lookup for both spawn paths, so the timeline and the debug swarm can never
    /// disagree about who is carrying.
    /// </summary>
    public EnemyType FindUpgradeCarrier()
    {
        var progress = PlayerProgress.Instance;
        if (progress != null && progress.Tier >= tuning.eliteCarrierFromTier)
        {
            var elite = FindType(tuning.eliteCarrierType);
            if (elite != null) return elite;
        }

        return FindType(tuning.upgradeCarrierType);
    }

    /// <summary>
    /// Somewhere just off screen for the boss to walk on from.
    ///
    /// This used to take the farthest clear point on the whole map perimeter, for the
    /// sake of the camera reveal. That was fine on an eight by eight city. On a
    /// sixteen by sixteen one it puts a 1.65 speed boss up to seventy units away and
    /// asks it to cross a city twice as dense as it used to be on greedy local
    /// avoidance — so it either arrives after the run is over or never arrives at all.
    ///
    /// Now it starts just outside the view and works outward, exactly like every other
    /// wave. The reveal still reads, because the camera pans to it either way.
    /// </summary>
    public bool TryFindBossSpawn(EnemyType type, out Vector3 position)
    {
        position = default;
        if (player == null) return false;

        float clearance = Mathf.Max(0.6f, type.bodyPx * 0.5f / tuning.pixelsPerUnit);
        var cam = Camera.main;
        float offScreen = cam != null ? cam.orthographicSize * cam.aspect + clearance + 2f : 18f;

        var land = Rect.MinMaxRect(landBounds.xMin + clearance, landBounds.yMin + clearance,
                                   landBounds.xMax - clearance, landBounds.yMax - clearance);
        if (land.width <= 0f || land.height <= 0f) return false;

        // Full clearance is a circle five and a half units across for the Abomination,
        // and blocks are three and a half apart on the short axis — insisting on that
        // inside the city would never find anything. A boss slightly overlapping a
        // rooftop on the frame it appears is not a problem; one that cannot spawn is.
        float needed = clearance * 0.6f;

        // A boss that walks through buildings only needs to be on land and off screen.
        // Holding it to clear ground as well would push it out to the beach every
        // time, purely to satisfy a collision it does not have.
        bool needsClearGround = !type.ignoresBuildings;

        float startAngle = Random.value * Mathf.PI * 2f;
        for (int ring = 0; ring < 8; ring++)
        {
            float r = offScreen + ring * clearance;
            for (int i = 0; i < 16; i++)
            {
                float a = startAngle + i / 16f * Mathf.PI * 2f;
                var at = player.transform.position
                       + new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r * tuning.isoSquash, 0f);

                if (!land.Contains(at)) continue;
                if (needsClearGround && Physics2D.OverlapCircle(at, needed) != null) continue;

                position = at;
                return true;
            }
        }

        // Nothing near enough was clear, so fall back to the closest open shoreline.
        // Closest, not farthest: a long walk is the failure this method exists to avoid.
        float best = float.MaxValue;
        bool found = false;
        for (int side = 0; side < 4; side++)
            for (int sample = 0; sample <= 32; sample++)
            {
                float t = sample / 32f;
                Vector2 at = side == 0 ? new Vector2(Mathf.Lerp(land.xMin, land.xMax, t), land.yMin)
                    : side == 1 ? new Vector2(land.xMax, Mathf.Lerp(land.yMin, land.yMax, t))
                    : side == 2 ? new Vector2(Mathf.Lerp(land.xMin, land.xMax, t), land.yMax)
                    : new Vector2(land.xMin, Mathf.Lerp(land.yMin, land.yMax, t));

                if (Physics2D.OverlapCircle(at, needed) != null) continue;

                var delta = at - (Vector2)player.transform.position;
                delta.y /= Mathf.Max(0.01f, tuning.isoSquash);
                float score = delta.sqrMagnitude;
                if (score >= best) continue;

                best = score;
                position = at;
                found = true;
            }

        if (!found) Debug.LogWarning("KRZ: no clear boss spawn anywhere on land.");
        return found;
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

    /// <summary>
    /// The size class a district leans toward. Not a rule — the weight roll only gets
    /// pushed, so every district still holds a spread and the city reads as one place
    /// with neighbourhoods rather than four tiled zones.
    ///
    /// Downtown is deliberately low-rise whichever quadrant it falls in. The run starts
    /// there, and the first minute has to be food you can actually reach: a wall of
    /// size-4 towers around the spawn point is a run that never gets going.
    /// </summary>
    int DistrictTarget(int bx, int by)
    {
        float nx = (bx + 0.5f) / tuning.blocksX * 2f - 1f;
        float ny = (by + 0.5f) / tuning.blocksY * 2f - 1f;

        if (Mathf.Max(Mathf.Abs(nx), Mathf.Abs(ny)) < tuning.downtownFraction) return 0;

        bool right = nx >= 0f, top = ny >= 0f;
        if (right && top) return 4;        // uptown: the skyline, and the late game
        if (!right && !top) return 0;      // the sprawl: low, dense, early food
        return 2;                          // everything else: mixed, leaning medium
    }

    /// <summary>
    /// Weight roll pushed toward a size class. Each class of distance from the target
    /// divides a type's chance by districtBias, so the far end of the range thins out
    /// rather than disappearing.
    /// </summary>
    BuildingType PickType(BuildingType[] types, System.Random rng, int target)
    {
        if (types == null || types.Length == 0) return null;

        float bias = Mathf.Max(1f, tuning.districtBias);
        float total = 0f;
        foreach (var t in types)
        {
            if (t.weight <= 0f) continue;
            total += t.weight / Mathf.Pow(bias, Mathf.Abs(t.sizeClass - target));
        }
        if (total <= 0f) return null;

        float roll = (float)rng.NextDouble() * total;
        foreach (var t in types)
        {
            if (t.weight <= 0f) continue;
            roll -= t.weight / Mathf.Pow(bias, Mathf.Abs(t.sizeClass - target));
            if (roll <= 0f) return t;
        }

        // Fall back to the last *weighted* type. Returning types[^1] would hand back a
        // zero-weight reactor on a floating-point edge, placing one outside the spacing
        // pass that exists to keep two off the same screen.
        for (int i = types.Length - 1; i >= 0; i--)
            if (types[i].weight > 0f) return types[i];
        return null;
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

        // Always present, and inert until the Toxin upgrade is held. Cheaper than
        // adding a component mid-run, and it keeps the pickup instant.
        var toxin = go.AddComponent<ToxicField>();
        toxin.tuning = tuning;

        // Art hangs off a child so growth scales the sprite without scaling the footprint.
        var art = new GameObject("Art").transform;
        art.SetParent(go.transform, false);

        var shadow = new GameObject("Shadow");
        shadow.transform.SetParent(art, false);
        var ssr = shadow.AddComponent<SpriteRenderer>();
        ssr.sprite = GreyboxArt.Shadow(160, ppu);
        ssr.sortingOrder = -1;

        // A soft halo under the body, so the tint below reads as something glowing
        // rather than as a repainted model. Sits on the art transform, so it grows
        // with the kaiju without anything having to drive it.
        var glowGo = new GameObject("Glow");
        glowGo.transform.SetParent(art, false);
        var glowSr = glowGo.AddComponent<SpriteRenderer>();
        glowSr.sprite = GreyboxArt.Cloud(128, ppu);
        glowSr.color = tuning.playerGlow;
        glowSr.sortingOrder = -2;   // behind the contact shadow as well as the body
        glowGo.transform.localPosition = new Vector3(0f, tuning.playerGlowHeight, 0f);

        // Cloud is a 2:1 ellipse, so the vertical scale is doubled to make the halo
        // round. Left as it comes, a glow meant to wrap a standing figure instead
        // pools on the floor around its feet.
        glowGo.transform.localScale = new Vector3(tuning.playerGlowSize,
                                                  tuning.playerGlowSize * 2f, 1f);

        var bodyGo = new GameObject("Body");
        bodyGo.transform.SetParent(art, false);
        var bsr = bodyGo.AddComponent<SpriteRenderer>();
        bsr.sprite = GreyboxArt.Capsule(96, 128, new Color(0.55f, 0.85f, 0.45f), ppu);

        // The kaiju is drawn almost entirely in white and pale grey, so a straight
        // multiply is enough to recolour it — white takes the tint exactly, and the
        // shading underneath survives as darker shades of the same hue.
        bsr.color = tuning.playerTint;

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
        var commander = FindUpgradeCarrier();
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
