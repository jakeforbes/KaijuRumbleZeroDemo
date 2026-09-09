using UnityEngine;

/// <summary>
/// A collectable upgrade lying on the ground. Unlike food it is not pulled toward
/// you — you have to go and get it, which is what makes it read as a decision
/// rather than something the vacuum happened to sweep up.
/// </summary>
public class UpgradePickup : MonoBehaviour
{
    static Sprite sprite;
    static Transform root;

    Tuning tuning;
    UpgradeType type;
    float bobPhase;
    Vector3 basePos;

    public static void Spawn(Tuning tuning, UpgradeType type, Vector3 at, float ppu)
    {
        if (type == null) return;
        if (root == null) root = new GameObject("Upgrades").transform;
        if (sprite == null) sprite = GreyboxArt.Pickup(84, Color.white, ppu);

        var go = new GameObject($"Upgrade_{type.displayName}");
        go.transform.SetParent(root, false);
        go.transform.position = at;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = type.colour;
        sr.sortingOrder = 2;   // above food, so a prize never hides under gems

        var p = go.AddComponent<UpgradePickup>();
        p.tuning = tuning;
        p.type = type;
        p.basePos = at;
        p.bobPhase = Random.value * 10f;
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null) return;

        transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.6f + bobPhase) * 0.12f);

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
