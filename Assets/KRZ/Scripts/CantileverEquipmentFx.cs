using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Metal fan housings on the walls and an azimuth-scanning dish on the roof.
/// Mounts are measured against source art; all scale, pivot and mirror changes follow the sprite.</summary>
[DefaultExecutionOrder(1010)]
public sealed class CantileverEquipmentFx : MonoBehaviour
{
    struct Mounts
    {
        public Vector2 fanA, fanB, dish;
        public float fanRadius;
        public Mounts(float ax, float ay, float bx, float by, float dx, float dy, float r)
        { fanA = new Vector2(ax, ay); fanB = new Vector2(bx, by); dish = new Vector2(dx, dy); fanRadius = r; }
    }
    // Normalized image coordinates, top-left origin. Broken upper walls lose their equipment.
    static readonly Dictionary<string, Mounts> Layouts = new()
    {
        ["cantilever_1x1_low_pristine_s"] = new Mounts(.585f,.485f, .585f,.568f, .38f,.286f, .041f),
        ["cantilever_1x1_high_pristine_s"] = new Mounts(.568f,.447f, .568f,.522f, .38f,.26f, .044f),
        ["cantilever_1x2_low_pristine_s"] = new Mounts(.562f,.586f, .634f,.55f, .565f,.289f, .029f),
        ["cantilever_1x2_high_pristine_s"] = new Mounts(.576f,.452f, .576f,.538f, .534f,.237f, .033f),
        ["cantilever_2x2_low_pristine_s"] = new Mounts(.59f,.53f, .665f,.49f, .557f,.277f, .032f),
        ["cantilever_2x2_high_pristine_s"] = new Mounts(.626f,.449f, .626f,.54f, .542f,.227f, .044f),
        ["cantilever_1x1_low_damaged_1_s"] = new Mounts(.58f,.58f, 0,0, .348f,.313f, .037f),
        ["cantilever_1x1_high_damaged_1_s"] = new Mounts(.583f,.453f, .583f,.522f, .342f,.21f, .042f),
        ["cantilever_1x2_low_damaged_1_s"] = new Mounts(.576f,.574f, .63f,.527f, .553f,.3f, .027f),
        ["cantilever_1x2_high_damaged_1_s"] = new Mounts(.594f,.454f, .594f,.538f, .457f,.232f, .033f),
        ["cantilever_2x2_low_damaged_1_s"] = new Mounts(.607f,.537f, .671f,.495f, .588f,.297f, .03f),
        ["cantilever_2x2_high_damaged_1_s"] = new Mounts(.632f,.434f, .632f,.524f, .393f,.211f, .042f)
    };
    static Material material;
    static readonly int Motion = Shader.PropertyToID("_Motion");
    static readonly int Fade = Shader.PropertyToID("_Fade");
    Building owner;
    SpriteRenderer source;
    BuildingType settings;
    Mesh mesh;
    MeshRenderer output;
    MaterialPropertyBlock properties;
    Sprite shown;
    bool flipX, flipY, supported;
    float lastSize = -1, seed;
    readonly Vector3[] vertices = new Vector3[12];
    readonly Vector2[] uv = new Vector2[12];
    readonly Vector2[] kinds = new Vector2[12];
    static readonly Vector2[] Corners = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

    public static void Attach(Building building, SpriteRenderer sprite, BuildingType type)
    {
        if (building.GetComponent<CantileverEquipmentFx>() != null) return;
        var fx = building.gameObject.AddComponent<CantileverEquipmentFx>();
        fx.owner = building; fx.source = sprite; fx.settings = type;
        fx.seed = Mathf.Repeat(building.transform.position.x * 2.71f + building.transform.position.y * 1.83f, 20);
        fx.properties = new MaterialPropertyBlock();
        var child = new GameObject("Cantilever - fans and rooftop dish");
        child.transform.SetParent(building.transform, false);
        fx.mesh = new Mesh { name = "Cantilever equipment" };
        child.AddComponent<MeshFilter>().sharedMesh = fx.mesh;
        fx.output = child.AddComponent<MeshRenderer>();
        fx.output.shadowCastingMode = ShadowCastingMode.Off;
        fx.output.receiveShadows = false;
        if (material == null) material = new Material(Resources.Load<Shader>("CantileverEquipment")) { name = "Cantilever equipment (shared)" };
        fx.output.sharedMaterial = material;
    }

    void LateUpdate()
    {
        if (output == null || source == null) return;
        if (!owner.IsAlive || !source.enabled || source.sprite == null || !settings.cantileverEquipmentEnabled)
        { output.enabled = false; return; }
        if (shown != source.sprite || flipX != source.flipX || flipY != source.flipY || lastSize != settings.cantileverEquipmentSize)
            Rebuild();
        output.enabled = supported;
        if (!supported) return;
        output.sortingLayerID = source.sortingLayerID;
        output.sortingOrder = source.sortingOrder + 2;
        properties.SetVector(Motion, new Vector4(Time.time * settings.cantileverFanSpeed,
            Time.time * settings.cantileverDishSpeed, seed, 0));
        properties.SetColor(Fade, source.color);
        output.SetPropertyBlock(properties);
    }

    void Rebuild()
    {
        shown = source.sprite; flipX = source.flipX; flipY = source.flipY;
        lastSize = settings.cantileverEquipmentSize;
        string key = shown.name.Substring(shown.name.LastIndexOf('/') + 1);
        supported = Layouts.TryGetValue(key, out var layout);
        if (!supported) return;
        Quad(0, layout.fanA, layout.fanRadius, false);
        Quad(1, layout.fanB, layout.fanB == Vector2.zero ? 0 : layout.fanRadius, false);
        Quad(2, layout.dish, layout.fanRadius * 1.9f, true);
        var triangles = new int[18];
        for (int q = 0; q < 3; q++)
        {
            int v = q * 4, t = q * 6;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.Clear(); mesh.vertices = vertices; mesh.uv = uv; mesh.uv2 = kinds;
        mesh.triangles = triangles; mesh.RecalculateBounds();
    }

    void Quad(int q, Vector2 mount, float radius, bool dish)
    {
        float r = radius * shown.rect.width * lastSize;
        Vector2 centre = new Vector2(mount.x * shown.rect.width, (1 - mount.y) * shown.rect.height);
        // Dish artwork's foot is near the bottom of its quad and rests on the roof mount.
        if (dish) centre.y += r * .76f;
        for (int i = 0; i < 4; i++)
        {
            Vector2 p = (Corners[i] * 2 - Vector2.one) * r;
            // A circle seen obliquely on the right-facing wall becomes a slanted ellipse.
            if (!dish) p = new Vector2(p.x * .76f, p.y + p.x * .36f);
            p = (centre + p - shown.pivot) / shown.pixelsPerUnit;
            if (flipX) p.x = -p.x;
            if (flipY) p.y = -p.y;
            vertices[q * 4 + i] = p;
            uv[q * 4 + i] = Corners[i];
            kinds[q * 4 + i] = new Vector2(dish ? 1 : 0, q * 2.17f);
        }
    }
    void OnDisable() { if (output != null) output.enabled = false; }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
