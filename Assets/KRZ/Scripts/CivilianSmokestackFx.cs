using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Roof-mounted chimneys with continuous, drifting smoke. The four civilian
/// art families used by the city have individually placed mounts for surviving roofs.</summary>
[DefaultExecutionOrder(1010)]
public sealed class CivilianSmokestackFx : MonoBehaviour
{
    // Source-image X/Y from the top left; Z is size as a fraction of image width.
    static Vector3 M(float x, float y, float size) => new Vector3(x,y,size);
    static readonly Dictionary<string, Vector3[]> Mounts = new()
    {
        ["civilian_1x1_pristine_s"] = new[] { M(.736f,.574f,.061f) },
        ["civilian_1x1_damaged_1_s"] = new[] { M(.736f,.6f,.059f) },
        ["civilian_1x2_pristine_s"] = new[] { M(.445f,.46f,.047f), M(.512f,.501f,.043f) },
        ["civilian_1x2_damaged_1_s"] = new[] { M(.435f,.441f,.047f), M(.512f,.501f,.043f) },
        ["civilian_1x2_damaged_2_s"] = new[] { M(.432f,.49f,.042f) },
        ["civilian_1x3_pristine_s"] = new[] { M(.426f,.37f,.038f), M(.626f,.568f,.035f) },
        ["civilian_1x3_damaged_1_s"] = new[] { M(.43f,.363f,.038f), M(.61f,.54f,.035f) },
        ["civilian_2x2_pristine_s"] = new[] { M(.515f,.526f,.047f), M(.577f,.375f,.043f) },
        ["civilian_2x2_damaged_1_s"] = new[] { M(.44f,.59f,.043f), M(.616f,.381f,.043f) }
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
    static readonly Vector2[] Corners = { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };

    public static void Attach(Building building, SpriteRenderer sprite, BuildingType type)
    {
        if (building.GetComponent<CivilianSmokestackFx>() != null) return;
        var fx = building.gameObject.AddComponent<CivilianSmokestackFx>();
        fx.owner = building; fx.source = sprite; fx.settings = type;
        fx.seed = Mathf.Repeat(building.transform.position.x * 3.17f + building.transform.position.y * 2.39f, 30f);
        fx.properties = new MaterialPropertyBlock();
        var child = new GameObject("Civilian roof - smokestacks and drifting smoke");
        child.transform.SetParent(building.transform, false);
        fx.mesh = new Mesh { name = "Civilian smokestacks" };
        child.AddComponent<MeshFilter>().sharedMesh = fx.mesh;
        fx.output = child.AddComponent<MeshRenderer>();
        fx.output.shadowCastingMode = ShadowCastingMode.Off;
        fx.output.receiveShadows = false;
        if (material == null) material = new Material(Resources.Load<Shader>("CivilianSmokestack")) { name = "Civilian smoke (shared)" };
        fx.output.sharedMaterial = material;
    }

    void LateUpdate()
    {
        if (output == null || source == null) return;
        if (!owner.IsAlive || !source.enabled || source.sprite == null || !settings.smokestacksEnabled)
        { output.enabled = false; return; }
        if (shown != source.sprite || flipX != source.flipX || flipY != source.flipY || lastSize != settings.smokestackSize) Rebuild();
        output.enabled = supported;
        if (!supported) return;
        output.sortingLayerID = source.sortingLayerID;
        output.sortingOrder = source.sortingOrder + 2;
        properties.SetVector(Motion, new Vector4(Time.time * settings.smokeSpeed, seed, settings.smokeDensity, 0));
        properties.SetColor(Fade, source.color);
        output.SetPropertyBlock(properties);
    }

    void Rebuild()
    {
        shown = source.sprite; flipX = source.flipX; flipY = source.flipY;
        lastSize = settings.smokestackSize;
        string key = shown.name.Substring(shown.name.LastIndexOf('/') + 1);
        supported = Mounts.TryGetValue(key, out var mounts);
        if (!supported) return;
        int count = mounts.Length;
        var vertices = new Vector3[count * 4]; var uv = new Vector2[count * 4];
        var phase = new Vector2[count * 4]; var triangles = new int[count * 6];
        for (int q = 0; q < count; q++)
        {
            Vector3 m = mounts[q];
            float r = m.z * shown.rect.width * lastSize;
            Vector2 centre = new Vector2(m.x * shown.rect.width, (1-m.y)*shown.rect.height + r*3*.9f);
            for (int i = 0; i < 4; i++)
            {
                Vector2 corner = Corners[i]*2 - Vector2.one;
                Vector2 p = (centre + new Vector2(corner.x*r, corner.y*r*3) - shown.pivot) / shown.pixelsPerUnit;
                if (flipX) p.x = -p.x;
                if (flipY) p.y = -p.y;
                vertices[q*4+i] = p; uv[q*4+i] = Corners[i]; phase[q*4+i] = new Vector2(q*3.71f,0);
            }
            int v = q*4, t = q*6;
            triangles[t]=v; triangles[t+1]=v+1; triangles[t+2]=v+2;
            triangles[t+3]=v; triangles[t+4]=v+2; triangles[t+5]=v+3;
        }
        mesh.Clear(); mesh.vertices=vertices; mesh.uv=uv; mesh.uv2=phase; mesh.triangles=triangles;
        mesh.RecalculateBounds();
    }
    void OnDisable() { if (output != null) output.enabled = false; }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
