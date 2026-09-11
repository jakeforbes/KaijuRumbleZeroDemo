using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Shared cyan player / orange enemy beams with hot cores and moving energy filaments.</summary>
public sealed class EnergyBeamFx : MonoBehaviour
{
    static Mesh quad;
    static Material material;
    static readonly int Tint = Shader.PropertyToID("_Tint");
    static readonly int Progress = Shader.PropertyToID("_Progress");
    static readonly int Aspect = Shader.PropertyToID("_Aspect");
    MeshRenderer output;
    MaterialPropertyBlock properties;
    float age, life;
    bool held;
    float width;

    /// <summary>
    /// The shader's filaments scroll on sin(x*34 - _Progress*32), so progress advanced
    /// by exactly one of these wraps with the flow perfectly continuous. Everything
    /// else _Progress drives stays put: the fade only begins at 0.36, so a held beam
    /// never dims, and thickness moves 6% across the cycle, which reads as the power
    /// fluctuating rather than as a seam.
    /// </summary>
    const float FlowCycle = 2f * Mathf.PI / 32f;
    const float FlowSeconds = 0.25f;

    public static void Show(Vector3 from, Vector3 to, Color colour, float width, float duration)
    {
        Vector3 delta = to-from;
        float length = delta.magnitude;
        if (length < .01f || width <= 0) return;
        var fx = Build(colour, width);
        fx.life = Mathf.Max(.05f, duration);
        fx.Place(from, delta, length);
    }

    /// <summary>
    /// A beam that stays lit until it is released, for an attack that sweeps rather
    /// than flashes. Show() spends its whole life animating towards a fade-out, which
    /// is right for the player's Blast and wrong for anything held for seconds.
    ///
    /// Returns a handle: call Aim every frame to point it, then Release. The caller
    /// owns it, so whatever is firing must release it when it dies or the beam is
    /// left burning in mid-air with nothing behind it.
    /// </summary>
    public static EnergyBeamFx Hold(Color colour, float width)
    {
        if (width <= 0f) return null;
        var fx = Build(colour, width);
        fx.held = true;

        // Nothing is drawn until the owner points it. Built unaimed it would sit at
        // the origin for a frame, which is a beam across the middle of the map.
        fx.output.enabled = false;
        return fx;
    }

    /// <summary>Points a held beam. Length may change between calls.</summary>
    public void Aim(Vector3 from, Vector3 to)
    {
        Vector3 delta = to - from;
        float length = delta.magnitude;
        if (length < .01f) return;
        Place(from, delta, length);
    }

    public void Release()
    {
        if (output != null) output.enabled = false;
        if (this != null) Destroy(gameObject);
    }

    static EnergyBeamFx Build(Color colour, float width)
    {
        if (quad == null)
        {
            quad = new Mesh { name = "Shared energy beam" };
            quad.vertices = new[] { new Vector3(0,-1,0), new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(0,1,0) };
            quad.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up };
            quad.triangles = new[] { 0,1,2,0,2,3 }; quad.RecalculateBounds();
        }
        if (material == null) material = new Material(Resources.Load<Shader>("EnergyBeam")) { name = "Energy beams (shared)" };
        var go = new GameObject("Glowing energy beam");
        go.AddComponent<MeshFilter>().sharedMesh=quad;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material; renderer.sortingOrder=3;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        var fx=go.AddComponent<EnergyBeamFx>();
        fx.output=renderer; fx.width=width; fx.properties=new MaterialPropertyBlock();
        fx.properties.SetColor(Tint,colour); fx.properties.SetFloat(Progress,0);
        return fx;
    }

    void Place(Vector3 from, Vector3 delta, float length)
    {
        transform.position = from;
        transform.right = delta / length;
        transform.localScale = new Vector3(length, width, 1f);
        properties.SetFloat(Aspect, length / width);
        output.SetPropertyBlock(properties);
        output.enabled = true;
    }

    void Update()
    {
        age+=Time.deltaTime;

        if (held)
        {
            properties.SetFloat(Progress, Mathf.Repeat(age / FlowSeconds, 1f) * FlowCycle);
            output.SetPropertyBlock(properties);
            return;
        }

        if(age>=life) { output.enabled=false; Destroy(gameObject); return; }
        properties.SetFloat(Progress,age/life); output.SetPropertyBlock(properties);
    }
}
