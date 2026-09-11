using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One additive mesh per building, anchored to bright coloured artwork pixels.
/// Samples are shared per sprite; animation never allocates or consumes gameplay randomness.</summary>
[DefaultExecutionOrder(1000)]
public sealed class BuildingAmbientFx : MonoBehaviour
{
    struct Anchor { public Vector2 uv; public Color colour; public float score; }
    static readonly Dictionary<Sprite, Anchor[]> Cache = new();
    static Material material;
    Building owner;
    SpriteRenderer source;
    BuildingType settings;
    Sprite shown;
    Anchor[] anchors;
    Mesh mesh;
    MeshRenderer output;
    SortingGroup group;
    Vector3[] vertices;
    Color[] colours;
    float phase;

    public static void Attach(Building owner, SpriteRenderer source, BuildingType settings)
    {
        if (owner.GetComponent<BuildingAmbientFx>() != null) return;
        var fx = owner.gameObject.AddComponent<BuildingAmbientFx>();
        fx.owner = owner;
        fx.source = source;
        fx.settings = settings;
        fx.phase = Mathf.Repeat(owner.transform.position.x * 7.13f + owner.transform.position.y * 3.79f, 6.28f);
        // Keep all overlay geometry in the building's ground-pivot sorting unit.
        fx.group = owner.GetComponent<SortingGroup>();
        if (fx.group == null) fx.group = owner.gameObject.AddComponent<SortingGroup>();
        fx.group.sortingLayerID = source.sortingLayerID;
        fx.group.sortingOrder = source.sortingOrder;
        var child = new GameObject("Ambient lights and motes");
        child.transform.SetParent(owner.transform, false);
        child.AddComponent<MeshFilter>();
        fx.output = child.AddComponent<MeshRenderer>();
        fx.output.sortingOrder = source.sortingOrder + 1;
        fx.output.shadowCastingMode = ShadowCastingMode.Off;
        fx.output.receiveShadows = false;
        if (material == null)
            material = new Material(Resources.Load<Shader>("BuildingAmbientGlow")) { name = "Building ambient glow (shared)" };
        fx.output.sharedMaterial = material;
        fx.mesh = new Mesh { name = "Building ambient lights" };
        fx.mesh.MarkDynamic();
        child.GetComponent<MeshFilter>().sharedMesh = fx.mesh;
    }

    void LateUpdate()
    {
        if (output == null || source == null) return;
        group.sortingLayerID = source.sortingLayerID;
        group.sortingOrder = source.sortingOrder;
        bool active = owner.IsAlive && source.enabled && settings.ambientVfxEnabled && source.sprite != null;
        output.enabled = active;
        if (!active) return;
        if (shown != source.sprite) Rebuild();
        if (!source.isVisible) return;
        float time = Time.time;
        for (int i = 0; i < anchors.Length; i++)
        {
            var a = anchors[i];
            var p = new Vector3((a.uv.x * shown.rect.width - shown.pivot.x) / shown.pixelsPerUnit,
                                (a.uv.y * shown.rect.height - shown.pivot.y) / shown.pixelsPerUnit, 0);
            if (source.flipX) p.x = -p.x;
            if (source.flipY) p.y = -p.y;
            float seed = phase + i * 2.399f;
            float pulse = 0.7f + 0.3f * Mathf.Sin(time * settings.ambientVfxPulseSpeed * Mathf.PI * 2 + seed);
            float radius = Mathf.Clamp(shown.bounds.size.x * 0.055f, 0.055f, 0.28f) * settings.ambientVfxSize;
            Color c = a.colour;
            c.a = settings.ambientVfxIntensity * source.color.a * pulse;
            Quad(i * 3, p, radius * (0.92f + 0.08f * pulse), c);
            for (int j = 0; j < 2; j++)
            {
                float life = Mathf.Repeat(time * (0.23f + i * 0.009f) + seed + j * 0.5f, 1);
                var drift = new Vector3(Mathf.Sin(life * 5 + seed + j) * radius * 0.7f, life * radius * 3f, 0);
                c.a = settings.ambientVfxIntensity * source.color.a * Mathf.Sin(life * Mathf.PI) * settings.ambientVfxParticles;
                Quad(i * 3 + j + 1, p + drift, radius * 0.19f, c);
            }
        }
        mesh.vertices = vertices;
        mesh.colors = colours;
        mesh.RecalculateBounds();
    }

    void Quad(int index, Vector3 p, float radius, Color colour)
    {
        int n = index * 4;
        vertices[n] = p + new Vector3(-radius, -radius, 0);
        vertices[n + 1] = p + new Vector3(radius, -radius, 0);
        vertices[n + 2] = p + new Vector3(radius, radius, 0);
        vertices[n + 3] = p + new Vector3(-radius, radius, 0);
        for (int k = 0; k < 4; k++) colours[n + k] = colour;
    }

    void Rebuild()
    {
        shown = source.sprite;
        if (!Cache.TryGetValue(shown, out anchors)) Cache[shown] = anchors = FindLights(shown);
        int quads = anchors.Length * 3;
        vertices = new Vector3[quads * 4];
        colours = new Color[vertices.Length];
        var uv = new Vector2[vertices.Length];
        var triangles = new int[quads * 6];
        for (int i = 0; i < quads; i++)
        {
            int v = i * 4, t = i * 6;
            uv[v] = Vector2.zero; uv[v + 1] = Vector2.right;
            uv[v + 2] = Vector2.one; uv[v + 3] = Vector2.up;
            triangles[t] = v; triangles[t + 1] = v + 1; triangles[t + 2] = v + 2;
            triangles[t + 3] = v; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        mesh.Clear(); mesh.vertices = vertices; mesh.uv = uv; mesh.triangles = triangles;
    }

    static Anchor[] FindLights(Sprite sprite)
    {
        // Small GPU copy avoids enabling Read/Write on the large source artwork.
        const int width = 64, height = 128;
        var previous = RenderTexture.active;
        var rt = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var sample = new Texture2D(width, height, TextureFormat.RGBA32, false);
        var candidates = new List<Anchor>();
        try
        {
            Graphics.Blit(sprite.texture, rt);
            RenderTexture.active = rt;
            sample.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            var pixels = sample.GetPixels();
            for (int y = 1; y < height - 1; y++)
            for (int x = 1; x < width - 1; x++)
            {
                Color c = pixels[y * width + x];
                Color.RGBToHSV(c, out _, out float saturation, out float value);
                // Exclude the blue-grey walls: lights are bright, saturated accents.
                if (c.a < 0.8f || value < 0.68f || saturation < 0.45f) continue;
                candidates.Add(new Anchor { uv = new Vector2((x + 0.5f) / width, (y + 0.5f) / height),
                    colour = new Color(c.r / value, c.g / value, c.b / value), score = value * saturation });
            }
        }
        finally { RenderTexture.active = previous; RenderTexture.ReleaseTemporary(rt); Destroy(sample); }
        candidates.Sort((a, b) => b.score.CompareTo(a.score));
        var chosen = new List<Anchor>();
        foreach (var a in candidates)
        {
            bool close = false;
            foreach (var b in chosen)
            {
                Vector2 distance = a.uv - b.uv;
                distance.y *= sprite.rect.height / sprite.rect.width;
                if (distance.sqrMagnitude < 0.012f) { close = true; break; }
            }
            if (close) continue;
            chosen.Add(a);
            if (chosen.Count == 6) break;
        }
        return chosen.ToArray();
    }

    void OnDisable() { if (output != null) output.enabled = false; }
    void OnDestroy() { if (mesh != null) Destroy(mesh); }
}
