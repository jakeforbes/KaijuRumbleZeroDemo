using UnityEngine;

/// <summary>
/// A smashable building. Difficulty is relative, not absolute: how long it takes
/// depends on the gap between your size and the building's class. Your own class is
/// a couple of seconds, one above is a real fight, two above is a wall you come back
/// to. Three visual states — pristine, damaged, rubble — with rubble walkable.
/// </summary>
public class Building : Damageable
{
    /// <summary>Above the ground tiles at -100, below everything that sorts by Y at 0.</summary>
    const int RubbleSortingOrder = -50;

    public Tuning tuning;

    public override bool IsAlive => hp > 0f;

    BuildingType type;
    int tilesX, tilesY;
    int fullHeightPx;
    float ppu;

    float hp;
    float maxHp;
    bool damagedShown;

    SpriteRenderer sr;
    PolygonCollider2D footprint;
    HealthBar bar;

    public void Init(Tuning t, BuildingType buildingType, int tx, int ty, int heightPx, float pixelsPerUnit)
    {
        tuning = t;
        type = buildingType;
        tilesX = tx;
        tilesY = ty;
        fullHeightPx = heightPx;
        ppu = pixelsPerUnit;

        sr = GetComponent<SpriteRenderer>();
        footprint = GetComponent<PolygonCollider2D>();

        maxHp = type.hp;
        hp = maxHp;
    }

    /// <summary>
    /// Multiplier from the size gap. Positive gap is the payoff for having grown,
    /// negative is the wall — and the wall is what makes growing worth doing.
    /// </summary>
    float DeltaMultiplier()
    {
        var table = tuning.damageVsBuildingByDelta;
        if (table == null || table.Length == 0) return 1f;

        int tier = PlayerProgress.Instance != null ? PlayerProgress.Instance.Tier : 0;
        int delta = tier - type.sizeClass;

        int centre = table.Length / 2;
        return table[Mathf.Clamp(centre + delta, 0, table.Length - 1)];
    }

    public override void TakeDamage(float amount, Vector2 from)
    {
        if (!IsAlive) return;

        hp -= amount * DeltaMultiplier();
        AudioEvents.Play(Sfx.BuildingHit, transform.position);

        if (hp <= 0f) { Collapse(); return; }

        if (!damagedShown && hp <= maxHp * 0.5f) ShowDamaged();
        ShowBar();
    }

    void ShowDamaged()
    {
        damagedShown = true;
        // Slumped and drained of colour, so the state reads at a glance in greybox.
        var faded = Color.Lerp(type.colour, new Color(0.30f, 0.30f, 0.33f), 0.45f);
        sr.sprite = GreyboxArt.IsoBox(tilesX, tilesY,
                                      Mathf.RoundToInt(fullHeightPx * 0.72f), faded, ppu);
    }

    /// <summary>Created on first damage, not up front — an intact building says nothing.</summary>
    void ShowBar()
    {
        if (!tuning.showBuildingHealthBars) return;

        if (bar == null)
        {
            float width = GreyboxArt.TileW * 0.5f * (tilesX + tilesY) / ppu * 0.5f;
            bar = HealthBar.Attach(transform, width, sr.sprite.bounds.max.y, ppu);
        }
        else
        {
            bar.Reposition(sr.sprite.bounds.max.y);
        }

        bar.Set(hp / maxHp);
    }

    void Collapse()
    {
        hp = 0f;
        AudioEvents.Play(Sfx.BuildingDestroyed, transform.position);

        if (bar != null) Destroy(bar.gameObject);

        var rubble = new Color(0.20f, 0.20f, 0.23f);
        int rubbleHeight = Mathf.RoundToInt(GreyboxArt.TileH * 0.5f * (tilesX + tilesY) * 0.22f);
        sr.sprite = GreyboxArt.IsoBox(tilesX, tilesY, rubbleHeight, rubble, ppu);

        // Taken off the Y-sort entirely. Rubble is walkable, so the player can stand
        // north of it, and Y-sorting would then draw debris over them. Anything you
        // can walk on should stay under you.
        sr.sortingOrder = RubbleSortingOrder;
        if (OccluderFade.Instance != null) OccluderFade.Instance.Unregister(sr);

        // Rubble is walkable, so the footprint goes away entirely.
        if (footprint != null) footprint.enabled = false;

        Food.Scatter(tuning, transform.position, type.foodDrops, type.foodScatter, ppu);

        // Stage 6 moves this onto Laboratories; for now any building can pay out so
        // the upgrade system is testable without the wave director.
        if (Random.value < tuning.upgradeDropChance && PlayerUpgrades.Instance != null)
            UpgradePickup.Spawn(tuning, PlayerUpgrades.Instance.RollDrop(), transform.position, ppu);
    }
}
