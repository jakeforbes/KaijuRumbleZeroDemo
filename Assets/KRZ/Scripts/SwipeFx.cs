using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Energy crescent on the attack's flat-ground arc. Damage and timing stay in PlayerAttack.</summary>
public class SwipeFx : MonoBehaviour
{
    static Mesh quad;
    static Material material;
    static readonly int Progress = Shader.PropertyToID("_Progress");
    static readonly int HalfArc = Shader.PropertyToID("_HalfArc");
    static readonly int EnergyColour = Shader.PropertyToID("_EnergyColour");
    MeshRenderer output;
    MaterialPropertyBlock properties;
    float life, age;

    public static void Show(Tuning tuning, Vector3 at, Vector2 aim, float range, float arc, float ppu)
    {
        if (!tuning.showSwipeArc || range <= 0) return;
        if (quad == null)
        {
            quad = new Mesh { name = "Shared energy swipe quad" };
            quad.vertices = new[] { new Vector3(-1,-1,0), new Vector3(1,-1,0), new Vector3(1,1,0), new Vector3(-1,1,0) };
            quad.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            quad.triangles = new[] { 0,1,2,0,2,3 };
            quad.RecalculateBounds();
        }
        if (material == null)
            material = new Material(Resources.Load<Shader>("SwipeEnergy")) { name = "Swipe energy (shared)" };

        // Rotate in the flat plane before squashing, exactly as the old wedge did.
        float squash = Mathf.Max(.01f, tuning.isoSquash);
        var root = new GameObject("swiping energy");
        root.transform.position = at;
        root.transform.localScale = new Vector3(1, squash, 1);
        Vector2 flat = new Vector2(aim.x, aim.y / squash);
        var child = new GameObject("energy crescent");
        child.transform.SetParent(root.transform, false);
        child.transform.localRotation = Quaternion.Euler(0,0,Mathf.Atan2(flat.y,flat.x)*Mathf.Rad2Deg);
        child.transform.localScale = Vector3.one * range;
        child.AddComponent<MeshFilter>().sharedMesh = quad;
        var renderer = child.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        renderer.sortingOrder = 2;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        var fx = root.AddComponent<SwipeFx>();
        fx.output = renderer;
        fx.life = Mathf.Max(.05f, tuning.swipeEnergyDuration);
        fx.properties = new MaterialPropertyBlock();
        fx.properties.SetFloat(HalfArc, Mathf.Clamp(arc,1,360)*.5f*Mathf.Deg2Rad);
        fx.properties.SetColor(EnergyColour, tuning.swipeEnergyColour);
        fx.properties.SetFloat(Progress, 0);
        renderer.SetPropertyBlock(fx.properties);
    }

    void Update()
    {
        age += Time.deltaTime;
        if (age >= life) { output.enabled = false; Destroy(gameObject); return; }
        properties.SetFloat(Progress, age / life);
        output.SetPropertyBlock(properties);
    }
}
