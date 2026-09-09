using UnityEngine;

/// <summary>
/// A dropped food pickup. Pops out of a collapsing building along an arc, lands,
/// bobs, then flies to the player once they are inside the pickup radius.
/// No prefabs — everything is built in code so nothing needs wiring.
/// </summary>
public class Food : MonoBehaviour
{
    static Sprite spriteSmall, spriteMedium, spriteLarge;
    static Transform root;

    Tuning tuning;
    float value;

    Vector3 from, to;
    float hopT;
    float hopTime;
    float hopHeight;
    float bobPhase;
    float spawnedAt;
    bool homing;
    float magnetSpeed;

    public static void Scatter(Tuning tuning, Vector3 at, int count, bool richer, float ppu)
    {
        EnsureSprites(ppu);
        // Unity's == catches a destroyed transform after a restart; ??= would not.
        if (root == null) root = new GameObject("Food").transform;

        for (int i = 0; i < count; i++)
        {
            int roll = Random.Range(0, 100);
            int tier = richer
                ? (roll < 45 ? 0 : roll < 85 ? 1 : 2)
                : (roll < 70 ? 0 : roll < 97 ? 1 : 2);

            Spawn(tuning, at, tier, richer ? tuning.foodScatterLarge : tuning.foodScatter);
        }
    }

    public static void Spawn(Tuning tuning, Vector3 at, int tier, float scatter)
    {
        var go = new GameObject("food");
        go.transform.SetParent(root, false);
        go.transform.position = at;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = tier == 2 ? spriteLarge : tier == 1 ? spriteMedium : spriteSmall;

        // Pickups always draw on top. Losing a gem behind rubble is worse than the
        // small cheat of it showing through a building.
        sr.sortingOrder = 1;

        var f = go.AddComponent<Food>();
        f.tuning = tuning;
        f.value = tier == 2 ? tuning.foodValueLarge
                : tier == 1 ? tuning.foodValueMedium
                            : tuning.foodValueSmall;

        // A real distance, not a velocity that decays to nothing. Vertical spread is
        // halved so the spray sits on the isometric ground plane. sqrt on the radius
        // spreads pieces evenly across the disc instead of clumping them at the centre.
        Vector2 dir = Random.insideUnitCircle.normalized * Mathf.Sqrt(Random.Range(0.12f, 1f));
        f.from = at;
        f.to = at + new Vector3(dir.x, dir.y * tuning.isoSquash, 0f) * scatter;
        f.hopTime = tuning.foodHopTime * Random.Range(0.85f, 1.25f);
        f.hopHeight = 0.3f + dir.magnitude * 0.5f;   // further throws arc higher
        f.bobPhase = Random.value * 10f;
        f.spawnedAt = Time.time;
    }

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null) return;

        if (!homing && hopT < 1f)
        {
            hopT = Mathf.Min(1f, hopT + Time.deltaTime / hopTime);
            // Ease out, plus an arc so it reads as thrown rather than slid.
            float e = 1f - (1f - hopT) * (1f - hopT);
            transform.position = Vector3.Lerp(from, to, e) + Vector3.up * (Mathf.Sin(hopT * Mathf.PI) * hopHeight);
            return;
        }

        Vector2 toPlayer = progress.transform.position - transform.position;

        // Compare on the unsquashed plane so the radius is a circle in world terms
        // rather than the ellipse the projection would otherwise make it.
        float flat = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).magnitude;

        if (!homing)
        {
            if (Time.time - spawnedAt < tuning.foodArmDelay) { Bob(); return; }
            if (flat > progress.PickupRadius) { Bob(); return; }
            homing = true;
            magnetSpeed = tuning.foodMagnetStartSpeed;
        }

        // Drifts off slowly and builds speed, so it reads as being pulled in rather
        // than snapped in. By the time it reaches you it is moving fast.
        magnetSpeed = Mathf.Min(tuning.foodMagnetMaxSpeed,
                                magnetSpeed + tuning.foodMagnetAccel * Time.deltaTime);

        transform.position = Vector2.MoveTowards(
            transform.position, progress.transform.position, magnetSpeed * Time.deltaTime);

        if (toPlayer.sqrMagnitude < 0.09f)
        {
            progress.AddFood(value);
            AudioEvents.Play(Sfx.FoodPickup, transform.position, 0.5f);
            Destroy(gameObject);
        }
    }

    void Bob()
    {
        var p = to;
        p.y += Mathf.Sin(Time.time * 3.2f + bobPhase) * 0.05f;
        transform.position = p;
    }

    static void EnsureSprites(float ppu)
    {
        if (spriteSmall != null) return;
        spriteSmall = GreyboxArt.Pickup(40, new Color(1f, 0.85f, 0.34f), ppu);
        spriteMedium = GreyboxArt.Pickup(58, new Color(1f, 0.62f, 0.22f), ppu);
        spriteLarge = GreyboxArt.Pickup(76, new Color(1f, 0.36f, 0.30f), ppu);
    }

    /// <summary>Cleared between runs so a restart does not reuse dead transforms.</summary>
    public static void Reset() => root = null;
}
