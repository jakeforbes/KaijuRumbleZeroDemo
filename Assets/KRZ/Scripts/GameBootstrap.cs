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

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Launch()
    {
        if (Instance != null) return;
        var go = new GameObject("~KRZ");
        Instance = go.AddComponent<GameBootstrap>();
    }

    public static void Restart() => SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);

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

        ClearScene();
        var cam = BuildCamera();
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

        var palette = new[]
        {
            new Color(0.24f, 0.28f, 0.40f),
            new Color(0.28f, 0.26f, 0.38f),
            new Color(0.21f, 0.30f, 0.38f),
            new Color(0.30f, 0.29f, 0.34f),
        };

        var root = new GameObject("City").transform;

        for (int by = 0; by < tuning.blocksY; by++)
            for (int bx = 0; bx < tuning.blocksX; bx++)
            {
                // Leave the centre clear so the player has room to start.
                if (Mathf.Abs(bx - tuning.blocksX / 2) <= 1 && Mathf.Abs(by - tuning.blocksY / 2) <= 1) continue;

                int tiles = rng.Next(0, 4) == 0 ? 2 : 1;
                int heightPx = tiles == 2 ? rng.Next(340, 760) : rng.Next(150, 380);
                var colour = palette[rng.Next(palette.Length)];

                float x = (bx - tuning.blocksX * 0.5f) * tuning.blockSpacingX;
                float y = (by - tuning.blocksY * 0.5f) * tuning.blockSpacingY;

                var go = new GameObject($"Building_{bx}_{by}");
                go.transform.SetParent(root, false);
                go.transform.position = new Vector3(x, y, 0f);

                var sr = go.AddComponent<SpriteRenderer>();
                sr.sprite = GreyboxArt.IsoBox(tiles, heightPx, colour, ppu);

                // Footprint only — never the sprite bounds, so the player can
                // overlap a tower's upper floors without colliding with them.
                var col = go.AddComponent<CapsuleCollider2D>();
                col.direction = CapsuleDirection2D.Horizontal;
                float w = GreyboxArt.TileW * tiles / ppu * 0.86f;
                col.size = new Vector2(w, w * 0.5f);
            }
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
        col.size = new Vector2(1.1f, 0.55f);

        var pc = go.AddComponent<PlayerController>();
        pc.tuning = tuning;

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

        pc.BindArt(art);
        return pc;
    }
}
