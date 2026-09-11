using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Two crawling electrical strands per visible roof coil. Authored regions
/// follow each damage frame, excluding missing coils and the front-panel sign.</summary>
[DefaultExecutionOrder(1010)]
public sealed class ReactorCoilFx : MonoBehaviour
{
    // Normalized source-image coordinates, measured from the TOP left.
    // Each vector is centre X, top Y, width, height of the exposed green winding.
    static Vector4 C(float x, float y, float w, float h) => new Vector4(x, y, w, h);
    static readonly Dictionary<string, Vector4[]> Coils = new()
    {
        ["reactor_1x1_pristine_s"] = new[] { C(.256f,.676f,.075f,.074f), C(.8f,.65f,.075f,.078f) },
        ["reactor_1x1_damaged_1_s"] = new[] { C(.256f,.676f,.075f,.074f), C(.796f,.645f,.075f,.073f) },
        ["reactor_1x1_damaged_2_s"] = new[] { C(.256f,.665f,.075f,.073f) },
        ["reactor_3x3_pristine_s"] = new[] { C(.548f,.168f,.085f,.118f), C(.25f,.388f,.085f,.107f), C(.819f,.423f,.085f,.103f), C(.509f,.607f,.08f,.1f) },
        ["reactor_3x3_damaged_1_s"] = new[] { C(.543f,.266f,.085f,.09f), C(.235f,.447f,.085f,.086f), C(.794f,.456f,.085f,.095f), C(.534f,.66f,.074f,.112f) },
        ["reactor_3x3_damaged_2_s"] = new[] { C(.245f,.51f,.075f,.07f), C(.804f,.572f,.077f,.065f), C(.504f,.741f,.055f,.085f) },
        ["reactor_2x2_pristine_s"] = new[] { C(.529f,.29f,.085f,.094f), C(.19f,.493f,.085f,.09f), C(.855f,.497f,.085f,.087f) },
        ["reactor_2x2_damaged_1_s"] = new[] { C(.529f,.365f,.085f,.09f), C(.196f,.547f,.085f,.087f), C(.85f,.512f,.085f,.086f) },
        ["reactor_2x2_damaged_2_s"] = new[] { C(.534f,.417f,.085f,.082f), C(.867f,.669f,.075f,.056f) },
        ["reactor_2x2_pristine_e"] = new[] { C(.545f,.161f,.1f,.13f), C(.214f,.422f,.083f,.103f), C(.542f,.466f,.1f,.128f), C(.876f,.417f,.075f,.112f) },
        ["reactor_2x2_damaged_1_e"] = new[] { C(.542f,.263f,.1f,.108f), C(.214f,.542f,.078f,.09f), C(.542f,.555f,.1f,.12f), C(.876f,.499f,.075f,.102f) },
        ["reactor_2x2_damaged_2_e"] = new[] { C(.553f,.33f,.085f,.083f), C(.876f,.594f,.075f,.075f) },
        ["reactor_2x2_pristine_n"] = new[] { C(.502f,.284f,.085f,.103f), C(.18f,.49f,.085f,.094f), C(.84f,.484f,.085f,.093f) },
        ["reactor_2x2_damaged_1_n"] = new[] { C(.502f,.364f,.085f,.082f), C(.18f,.55f,.085f,.09f), C(.84f,.535f,.085f,.095f) },
        ["reactor_2x2_damaged_2_n"] = new[] { C(.502f,.432f,.085f,.082f) },
        ["reactor_2x2_pristine_ne"] = new[] { C(.477f,.305f,.085f,.084f), C(.178f,.492f,.085f,.093f), C(.811f,.462f,.085f,.084f) },
        ["reactor_2x2_damaged_1_ne"] = new[] { C(.477f,.375f,.085f,.071f), C(.178f,.54f,.085f,.093f), C(.811f,.512f,.085f,.084f) },
        ["reactor_2x2_damaged_2_ne"] = new[] { C(.479f,.425f,.085f,.063f), C(.24f,.67f,.061f,.033f) },
        ["reactor_2x2_pristine_se"] = new[] { C(.558f,.261f,.085f,.084f), C(.2f,.432f,.085f,.088f), C(.886f,.463f,.085f,.098f) },
        ["reactor_2x2_damaged_1_se"] = new[] { C(.565f,.347f,.085f,.069f), C(.203f,.522f,.085f,.094f), C(.896f,.522f,.085f,.085f) },
        ["reactor_2x2_damaged_2_se"] = new[] { C(.57f,.375f,.085f,.08f), C(.883f,.655f,.078f,.05f) }
    };
    const int Segments = 18;
    static Material material;
    Building owner;
    SpriteRenderer source;
    BuildingType settings;
    MeshRenderer output;
    Mesh mesh;
    Sprite shown;
    Vector4[] regions;
    Vector3[] vertices;
    Color[] colours;
    float seed;

    public static void Attach(Building building, SpriteRenderer sprite, BuildingType type)
    {
        if (building.GetComponent<ReactorCoilFx>() != null) return;
        var fx = building.gameObject.AddComponent<ReactorCoilFx>();
        fx.owner = building; fx.source = sprite; fx.settings = type;
        fx.seed = Mathf.Repeat(building.transform.position.x * 1.73f + building.transform.position.y * 3.11f, 20f);
        var child = new GameObject("Green lightning - roof coils");
        child.transform.SetParent(building.transform, false);
        fx.mesh = new Mesh { name = "Reactor coil lightning" };
        fx.mesh.MarkDynamic();
        child.AddComponent<MeshFilter>().sharedMesh = fx.mesh;
        fx.output = child.AddComponent<MeshRenderer>();
        fx.output.sortingLayerID = sprite.sortingLayerID;
        fx.output.sortingOrder = sprite.sortingOrder + 2;
        fx.output.shadowCastingMode = ShadowCastingMode.Off;
        fx.output.receiveShadows = false;
        if (material == null) material = new Material(Resources.Load<Shader>("ReactorCoilLightning"));
        fx.output.sharedMaterial = material;
    }

    void LateUpdate()
    {
        if (output == null || source == null) return;
        output.enabled = owner.IsAlive && source.enabled && source.sprite != null && settings.coilLightningEnabled;
        if (!output.enabled) return;
        if (shown != source.sprite) Rebuild();
        if (regions == null || !source.isVisible) return;
        float time = Time.time * settings.coilLightningSpeed;
        int quad = 0;
        for (int coil = 0; coil < regions.Length; coil++)
        for (int strand = 0; strand < 2; strand++)
        {
            Vector4 r = regions[coil];
            float phase = seed + coil * 2.399f + strand * Mathf.PI;
            float flicker = .65f + .35f * Mathf.PerlinNoise(phase * 3, time * 17);
            float alpha = source.color.a * settings.coilLightningIntensity * flicker;
            Vector3 previous = Point(r, 0, time, phase);
            float width = r.z * shown.rect.width / shown.pixelsPerUnit;
            for (int s = 1; s <= Segments; s++)
            {
                float t = s / (float)Segments;
                Vector3 next = Point(r, t, time, phase);
                // Dim the far side of the winding, so the arc appears to curl around it.
                float front = Mathf.Lerp(.18f, 1, (Mathf.Cos(t * Mathf.PI * 4 + time * 3 + phase) + 1) * .5f);
                Segment(quad++, previous, next, width * .15f, new Color(.02f, 1f, .16f, alpha * front * .55f));
                Segment(quad++, previous, next, width * .028f, new Color(.55f, 1f, .68f, alpha * front));
                previous = next;
            }
        }
        mesh.vertices = vertices; mesh.colors = colours; mesh.RecalculateBounds();
    }

    Vector3 Point(Vector4 r, float t, float time, float phase)
    {
        float tick = Mathf.Floor(time * 14);
        float jitter = (Mathf.PerlinNoise(t * 43 + phase, tick + seed) - .5f) * .22f;
        float x = r.x + (Mathf.Sin(t * Mathf.PI * 4 + time * 3 + phase) * .48f + jitter) * r.z;
        float y = 1 - (r.y + t * r.w);
        var p = new Vector3((x * shown.rect.width - shown.pivot.x) / shown.pixelsPerUnit,
                            (y * shown.rect.height - shown.pivot.y) / shown.pixelsPerUnit, 0);
        if (source.flipX) p.x = -p.x;
        if (source.flipY) p.y = -p.y;
        return p;
    }

    void Segment(int q, Vector3 a, Vector3 b, float width, Color c)
    {
        Vector3 d = b - a;
        Vector3 n = new Vector3(-d.y, d.x, 0).normalized * width;
        int v = q * 4;
        vertices[v] = a - n; vertices[v + 1] = b - n;
        vertices[v + 2] = b + n; vertices[v + 3] = a + n;
        for (int i = 0; i < 4; i++) colours[v + i] = c;
    }

    void Rebuild()
    {
        shown = source.sprite;
        string key = shown.name.Substring(shown.name.LastIndexOf('/') + 1);
        Coils.TryGetValue(key, out regions);
        int count = (regions == null ? 0 : regions.Length) * 2 * Segments * 2;
        vertices = new Vector3[count * 4]; colours = new Color[count * 4];
        var uv = new Vector2[count * 4]; var indices = new int[count * 6];
        for (int i = 0; i < count; i++)
        {
            int v = i * 4, n = i * 6;
            uv[v] = Vector2.zero; uv[v + 1] = Vector2.right; uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
            indices[n] = v; indices[n + 1] = v + 1; indices[n + 2] = v + 2;
            indices[n + 3] = v; indices[n + 4] = v + 2; indices[n + 5] = v + 3;
        }
        mesh.Clear(); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = indices;
    }
    void OnDisable() { if (output != null) output.enabled = false; }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
