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

    public static void Show(Vector3 from, Vector3 to, Color colour, float width, float duration)
    {
        Vector3 delta = to-from;
        float length = delta.magnitude;
        if (length < .01f || width <= 0) return;
        if (quad == null)
        {
            quad = new Mesh { name = "Shared energy beam" };
            quad.vertices = new[] { new Vector3(0,-1,0), new Vector3(1,-1,0),new Vector3(1,1,0),new Vector3(0,1,0) };
            quad.uv = new[] { Vector2.zero,Vector2.right,Vector2.one,Vector2.up };
            quad.triangles = new[] { 0,1,2,0,2,3 }; quad.RecalculateBounds();
        }
        if (material == null) material = new Material(Resources.Load<Shader>("EnergyBeam")) { name = "Energy beams (shared)" };
        var go = new GameObject("Glowing energy beam");
        go.transform.position=from; go.transform.right=delta/length;
        go.transform.localScale=new Vector3(length,width,1);
        go.AddComponent<MeshFilter>().sharedMesh=quad;
        var renderer=go.AddComponent<MeshRenderer>();
        renderer.sharedMaterial=material; renderer.sortingOrder=3;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        var fx=go.AddComponent<EnergyBeamFx>();
        fx.output=renderer; fx.life=Mathf.Max(.05f,duration); fx.properties=new MaterialPropertyBlock();
        fx.properties.SetColor(Tint,colour); fx.properties.SetFloat(Progress,0);
        fx.properties.SetFloat(Aspect,length/width);
        renderer.SetPropertyBlock(fx.properties);
    }
    void Update()
    {
        age+=Time.deltaTime;
        if(age>=life) { output.enabled=false; Destroy(gameObject); return; }
        properties.SetFloat(Progress,age/life); output.SetPropertyBlock(properties);
    }
}
