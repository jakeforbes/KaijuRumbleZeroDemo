using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A collectable upgrade lying on the ground. Unlike food it is not pulled toward
/// you — you have to go and get it, which is what makes it read as a decision
/// rather than something the vacuum happened to sweep up.
/// </summary>
public class UpgradePickup : MonoBehaviour
{
    static Mesh iconMesh;
    static Material iconMaterial;
    static Transform root;
    static readonly int Tint = Shader.PropertyToID("_Tint");
    static readonly int Phase = Shader.PropertyToID("_Phase");

    Tuning tuning;
    UpgradeType type;
    float bobPhase;
    Vector3 basePos;
    MeshRenderer icon;
    MaterialPropertyBlock properties;

    public static void Spawn(Tuning tuning, UpgradeType type, Vector3 at, float ppu)
    {
        if (type == null) return;
        if (root == null) root = new GameObject("Upgrades").transform;
        if (iconMesh == null)
        {
            iconMesh = new Mesh { name = "Shared DNA upgrade icon" };
            iconMesh.vertices = new[] { new Vector3(-.5f,-.5f,0), new Vector3(.5f,-.5f,0),
                                       new Vector3(.5f,.5f,0), new Vector3(-.5f,.5f,0) };
            iconMesh.uv = new[] { Vector2.zero, Vector2.right, Vector2.one, Vector2.up };
            iconMesh.triangles = new[] { 0,1,2,0,2,3 };
            iconMesh.RecalculateBounds();
        }
        if (iconMaterial == null)
            iconMaterial = new Material(Resources.Load<Shader>("UpgradeDna")) { name = "Upgrade DNA (shared)" };

        var go = new GameObject($"Upgrade_{type.displayName}");
        go.transform.SetParent(root, false);
        go.transform.position = at;

        var visual = new GameObject("Animated DNA");
        visual.transform.SetParent(go.transform, false);
        float size = 100f / Mathf.Max(1f, ppu);
        visual.transform.localScale = new Vector3(size * .75f, size, 1);
        visual.transform.localPosition = Vector3.up * size * .25f;
        visual.AddComponent<MeshFilter>().sharedMesh = iconMesh;
        var renderer = visual.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = iconMaterial;
        renderer.sortingOrder = 2; // above food, as before
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;

        var p = go.AddComponent<UpgradePickup>();
        p.tuning = tuning;
        p.type = type;
        p.basePos = at;
        p.bobPhase = Random.value * 10f;
        p.icon = renderer;
        p.properties = new MaterialPropertyBlock();
        p.properties.SetColor(Tint, type.colour);
        p.properties.SetFloat(Phase, Time.time * 2.4f + p.bobPhase);
        renderer.SetPropertyBlock(p.properties);
    }

    void Update()
    {
        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.6f + bobPhase) * 0.12f);
        properties.SetFloat(Phase, Time.time * 2.4f + bobPhase);
        properties.SetColor(Tint, type.colour);
        icon.SetPropertyBlock(properties);

        var progress = PlayerProgress.Instance;
        if (progress == null || progress.HasWon) return;

        Vector2 d = progress.transform.position - basePos;
        float flat = new Vector2(d.x, d.y / tuning.isoSquash).magnitude;

        if (flat <= tuning.upgradePickupRange * progress.Scale)
        {
            PlayerUpgrades.Instance?.Grant(type.id);
            Destroy(gameObject);
        }
    }

    public static void Reset() => root = null;
}
