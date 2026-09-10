using UnityEngine;

/// <summary>
/// A smashable building. Difficulty is relative, not absolute: how long it takes
/// depends on the gap between your size and the building's class. Your own class is
/// a couple of seconds, one above is a real fight, two above is a wall you come back
/// to. Three visual states — pristine, damaged, rubble — with rubble walkable.
/// </summary>
[SoundActions(Sfx.BuildingHit, Sfx.BuildingDestroyed, Sfx.ReactorPulse)]
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
        SoundPlayer.Attach(gameObject, t.buildingSounds);
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
        AudioEvents.Play(Sfx.BuildingHit, transform.position, owner: gameObject);

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

    /// <summary>
    /// Labs pay out a fixed number of power-ups. Everything else falls back to the
    /// chance on the Tuning asset, which is 0 now that Labs exist — it stays as a
    /// knob for making the whole city drop upgrades while testing.
    /// </summary>
    void DropUpgrades()
    {
        if (PlayerUpgrades.Instance == null) return;

        int drops = type.upgradeDrops;
        if (drops == 0 && Random.value < tuning.upgradeDropChance) drops = 1;

        for (int i = 0; i < drops; i++)
        {
            // Spread multiples so two prizes never land on the same pixel.
            var offset = drops > 1
                ? new Vector3(Random.Range(-1.2f, 1.2f), Random.Range(-0.6f, 0.6f), 0f)
                : Vector3.zero;
            UpgradePickup.Spawn(tuning, PlayerUpgrades.Instance.RollDrop(),
                                transform.position + offset, ppu);
        }
    }

    /// <summary>
    /// Reactor detonation. Enemies only — buildings and the player are untouched, so
    /// felling one is always a reward and never a risk. Iterated backwards because
    /// a kill removes the enemy from the list mid-loop.
    /// </summary>
    void Pulse()
    {
        AudioEvents.Play(Sfx.ReactorPulse, transform.position, owner: gameObject);
        HitFx.Burst(transform.position, new Color(1f, 0.85f, 0.35f),
                    type.pulseRadius * 0.85f, ppu, 0.45f);

        if (PlayerProgress.Instance != null)
            PlayerProgress.Instance.ShakeExternal(tuning.tierUpShake);

        for (int i = Enemy.All.Count - 1; i >= 0; i--)
        {
            var e = Enemy.All[i];
            if (e == null || !e.IsAlive) continue;

            Vector2 d = (Vector2)e.transform.position - (Vector2)transform.position;
            float flat = new Vector2(d.x, d.y / tuning.isoSquash).magnitude;
            if (flat <= type.pulseRadius) e.TakeDamage(type.pulseDamage, transform.position);
        }
    }

    void Collapse()
    {
        hp = 0f;
        AudioEvents.Play(Sfx.BuildingDestroyed, transform.position, owner: gameObject);

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
        DropUpgrades();
        if (type.pulseDamage > 0f) Pulse();
    }
}
