using UnityEngine;
using UnityEngine.Rendering;

/// <summary>A contained, luminous liquid mass under the lab dome. Both current lab
/// sizes use laboratory_2x2 art; sprite PPU handles their different world scales.</summary>
[DefaultExecutionOrder(1010)]
public sealed class LabDomeFx : MonoBehaviour
{
    static Material material;
    static readonly int MainTex = Shader.PropertyToID("_MainTex");
    static readonly int Dome = Shader.PropertyToID("_Dome");
    static readonly int Animation = Shader.PropertyToID("_Animation");
    static readonly int GlobSize = Shader.PropertyToID("_GlobSize");
    Building owner;
    SpriteRenderer source;
    BuildingType settings;
    MeshRenderer output;
    Mesh mesh;
    MaterialPropertyBlock properties;
    Sprite shown;
    bool flippedX, flippedY, hasDome;
    float seed;
    readonly Vector3[] vertices = new Vector3[4];
    static readonly Vector2[] UV = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

    public static void Attach(Building building, SpriteRenderer sprite, BuildingType type)
    {
        if (building.GetComponent<LabDomeFx>() != null) return;
        var fx = building.gameObject.AddComponent<LabDomeFx>();
        fx.owner = building; fx.source = sprite; fx.settings = type;
        fx.seed = Mathf.Repeat(building.transform.position.x * 2.37f + building.transform.position.y * 4.19f, 40f);
        fx.properties = new MaterialPropertyBlock();
        var child = new GameObject("Dome - floating luminous glob");
        child.transform.SetParent(building.transform, false);
        fx.mesh = new Mesh { name = "Lab dome liquid window" };
        fx.mesh.vertices = fx.vertices;
        fx.mesh.uv = UV;
        fx.mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
        child.AddComponent<MeshFilter>().sharedMesh = fx.mesh;
        fx.output = child.AddComponent<MeshRenderer>();
        fx.output.shadowCastingMode = ShadowCastingMode.Off;
        fx.output.receiveShadows = false;
        if (material == null) material = new Material(Resources.Load<Shader>("LabDomeGlob")) { name = "Lab dome glob (shared)" };
        fx.output.sharedMaterial = material;
    }

    void LateUpdate()
    {
        if (output == null || source == null) return;
        if (!owner.IsAlive || !source.enabled || source.sprite == null || !settings.labGlobEnabled)
        {
            output.enabled = false;
            return;
        }
        if (shown != source.sprite || flippedX != source.flipX || flippedY != source.flipY) Rebuild();
        output.enabled = hasDome;
        if (!hasDome) return;
        output.sortingLayerID = source.sortingLayerID;
        output.sortingOrder = source.sortingOrder + 2;
        properties.SetVector(Animation, new Vector4(Time.time * settings.labGlobSpeed, seed,
            settings.labGlobIntensity * source.color.a, 0));
        properties.SetFloat(GlobSize, settings.labGlobSize);
        output.SetPropertyBlock(properties);
    }

    void Rebuild()
    {
        shown = source.sprite; flippedX = source.flipX; flippedY = source.flipY;
        string key = shown.name.Substring(shown.name.LastIndexOf('/') + 1);
        // Centre X/Y and full width/height of the safe glass interior, from top left.
        Vector4 region;
        switch (key)
        {
            case "laboratory_2x2_pristine_s": region = new Vector4(.55f, .615f, .36f, .24f); break;
            case "laboratory_2x2_damaged_1_s": region = new Vector4(.545f, .565f, .34f, .23f); break;
            case "laboratory_2x2_damaged_2_s": region = new Vector4(.535f, .545f, .30f, .20f); break;
            default: hasDome = false; return;
        }
        hasDome = true;
        region.y = 1 - region.y;
        for (int i = 0; i < 4; i++)
        {
            Vector2 uv = new Vector2(region.x + (UV[i].x - .5f) * region.z,
                                     region.y + (UV[i].y - .5f) * region.w);
            var p = new Vector3((uv.x * shown.rect.width - shown.pivot.x) / shown.pixelsPerUnit,
                                (uv.y * shown.rect.height - shown.pivot.y) / shown.pixelsPerUnit, 0);
            if (flippedX) p.x = -p.x;
            if (flippedY) p.y = -p.y;
            vertices[i] = p;
        }
        mesh.vertices = vertices;
        mesh.RecalculateBounds();
        properties.SetTexture(MainTex, shown.texture);
        properties.SetVector(Dome, region);
    }

    void OnDisable() { if (output != null) output.enabled = false; }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
