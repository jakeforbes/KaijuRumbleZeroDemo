using UnityEngine;

/// <summary>
/// A dropped food pickup. Pops out of a collapsing building along an arc, lands,
/// bobs, then flies to the player once they are inside the pickup radius.
/// No prefabs — everything is built in code so nothing needs wiring.
/// </summary>
[SoundActions(Sfx.FoodPickup)]
public class Food : MonoBehaviour
{
    static Sprite spriteSmall, spriteMedium, spriteLarge;
    static Transform root;

    Tuning tuning;
    float value;

    Vector3 from, to;
    Vector3 pos;          // logical ground position; the bob is applied on top
    float hopT;
    float hopTime;
    float hopHeight;
    float bobPhase;
    float spawnedAt;
    float magnetSpeed;

    public static void Scatter(Tuning tuning, Vector3 at, int count, float scatter, float ppu)
    {
        EnsureSprites(ppu);
        // Unity's == catches a destroyed transform after a restart; ??= would not.
        if (root == null) root = new GameObject("Food").transform;

        // Bigger buildings throw further and pay better, so the mix richens with spread.
        bool richer = scatter >= 4.5f;

        for (int i = 0; i < count; i++)
        {
            int roll = Random.Range(0, 100);
            int tier = richer
                ? (roll < 45 ? 0 : roll < 85 ? 1 : 2)
                : (roll < 70 ? 0 : roll < 97 ? 1 : 2);

            Spawn(tuning, at, tier, scatter);
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
        SoundPlayer.Attach(go, tuning.foodSounds);
        f.value = tier == 2 ? tuning.foodValueLarge
                : tier == 1 ? tuning.foodValueMedium
                            : tuning.foodValueSmall;

        // A real distance, not a velocity that decays to nothing. Vertical spread is
        // halved so the spray sits on the isometric ground plane. sqrt on the radius
        // spreads pieces evenly across the disc instead of clumping them at the centre.
        Vector2 dir = Random.insideUnitCircle.normalized * Mathf.Sqrt(Random.Range(0.12f, 1f));
        f.from = at;
        f.pos = at;
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

        // Thrown out of the collapse, not yet settled.
        if (hopT < 1f)
        {
            hopT = Mathf.Min(1f, hopT + Time.deltaTime / hopTime);
            float e = 1f - (1f - hopT) * (1f - hopT);   // ease out
            pos = Vector3.Lerp(from, to, e);
            transform.position = pos + Vector3.up * (Mathf.Sin(hopT * Mathf.PI) * hopHeight);
            return;
        }

        Vector3 playerPos = progress.transform.position;
        Vector2 toPlayer = playerPos - pos;

        // Measure on the unsquashed plane, so the field is a circle in world terms
        // rather than the ellipse the projection would otherwise make of it.
        float flat = new Vector2(toPlayer.x, toPlayer.y / tuning.isoSquash).magnitude;
        float radius = progress.InfluenceRadius;

        // Strength rises from nothing at the edge of the field to full at the player.
        // The exponent is what makes distant food barely stir while close food is
        // hauled in — a linear falloff reads as one uniform vacuum.
        float wanted = 0f;
        if (Time.time - spawnedAt >= tuning.foodArmDelay && flat < radius)
        {
            float t = 1f - flat / radius;
            wanted = tuning.foodMagnetMaxSpeed * Mathf.Pow(t, tuning.foodPullFalloff);
        }

        magnetSpeed = Mathf.MoveTowards(magnetSpeed, wanted, tuning.foodMagnetAccel * Time.deltaTime);
        if (magnetSpeed > 0.001f)
            pos = Vector3.MoveTowards(pos, playerPos, magnetSpeed * Time.deltaTime);

        // Bob rides on top of the pulled position rather than fighting it.
        transform.position = pos + Vector3.up * (Mathf.Sin(Time.time * 3.2f + bobPhase) * 0.05f);

        if (toPlayer.sqrMagnitude < 0.09f)
        {
            progress.AddFood(value);
            // Player-owned settings follow the collector's size, including at max tier.
            // Fall back to the food object when the player has no pickup sound.
            if (!AudioEvents.Play(Sfx.FoodPickup, progress.transform.position, owner: progress.gameObject))
                AudioEvents.Play(Sfx.FoodPickup, transform.position, 0.5f, owner: gameObject);
            Destroy(gameObject);
        }
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
