using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Expanding, fading pressure rings on the ground plane. Cosmetic only.</summary>
public sealed class ShockwaveFx : MonoBehaviour
{
    public static readonly System.Collections.Generic.List<ShockwaveFx> Distortions = new();
    public float NormalizedAge => Mathf.Clamp01(age / life);
    static Mesh quad;
    static Material material;
    static readonly int Progress = Shader.PropertyToID("_Progress");
    static readonly int Tint = Shader.PropertyToID("_Tint");
    MeshRenderer output;
    MaterialPropertyBlock properties;
    float age, life;
    bool unscaled;

    public static void Show(Vector3 at, Color colour, float radius, float squash, float duration = .65f, bool displacement = false, bool unscaled = false)
    {
        if (radius <= 0) return;
        if (quad == null)
        {
            quad = new Mesh { name = "Shared shockwave quad" };
            quad.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0,1,2,0,2,3 };
            quad.RecalculateBounds();
        }
        if (material == null) material = new Material(Resources.Load<Shader>("ExplosionShockwave")) { name = "Explosion shockwave (shared)" };
        var go = new GameObject("Explosion - expanding shockwave");
        go.transform.position = at;
        go.transform.localScale = new Vector3(radius, radius * Mathf.Max(.01f, squash), 1);
        go.AddComponent<MeshFilter>().sharedMesh = quad;
        var renderer = go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = unscaled ? 1000 : 3;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var fx = go.AddComponent<ShockwaveFx>();
        fx.output = renderer; fx.life = Mathf.Max(.05f, duration);
        fx.unscaled = unscaled;
        fx.properties = new MaterialPropertyBlock();
        fx.properties.SetColor(Tint, colour);
        fx.properties.SetFloat(Progress, 0);
        renderer.SetPropertyBlock(fx.properties);
        if (displacement) Distortions.Add(fx);
    }
    void Update()
    {
        age += unscaled ? Time.unscaledDeltaTime : Time.deltaTime;
        if (age >= life) { output.enabled = false; Destroy(gameObject); return; }
        properties.SetFloat(Progress, age / life);
        output.SetPropertyBlock(properties);
    }
    void OnDestroy() => Distortions.Remove(this);
}
