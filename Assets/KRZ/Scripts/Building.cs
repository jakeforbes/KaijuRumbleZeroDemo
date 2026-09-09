using UnityEngine;

/// <summary>
/// A smashable building. Three visual states, matching the art spec: pristine,
/// damaged, then rubble. Rubble keeps its sprite but drops its collider so it
/// becomes walkable ground.
/// </summary>
public class Building : Damageable
{
    public Tuning tuning;

    public override bool IsAlive => hp > 0f;

    int tiles;
    int fullHeightPx;
    Color baseColour;
    float ppu;

    float hp;
    float maxHp;
    bool damagedShown;

    SpriteRenderer sr;
    PolygonCollider2D footprint;
    HealthBar bar;

    public void Init(Tuning t, int tileCount, int heightPx, Color colour, float pixelsPerUnit)
    {
        tuning = t;
        tiles = tileCount;
        fullHeightPx = heightPx;
        baseColour = colour;
        ppu = pixelsPerUnit;

        sr = GetComponent<SpriteRenderer>();
        footprint = GetComponent<PolygonCollider2D>();

        maxHp = tiles >= 2 ? tuning.buildingHpLarge : tuning.buildingHpSmall;
        hp = maxHp;
    }

    public override void TakeDamage(float amount, Vector2 from)
    {
        if (!IsAlive) return;

        hp -= amount;
        AudioEvents.Play(Sfx.BuildingHit, transform.position);

        if (hp <= 0f) { Collapse(); return; }

        if (!damagedShown && hp <= maxHp * 0.5f) ShowDamaged();
        ShowBar();
    }

    /// <summary>Created on first damage, not up front — an intact building says nothing.</summary>
    void ShowBar()
    {
        if (!tuning.showBuildingHealthBars) return;

        if (bar == null)
        {
            float width = GreyboxArt.TileW * tiles / ppu * 0.55f;
            bar = HealthBar.Attach(transform, width, sr.sprite.bounds.max.y, ppu);
        }
        else
        {
            bar.Reposition(sr.sprite.bounds.max.y);
        }

        bar.Set(hp / maxHp);
    }

    void ShowDamaged()
    {
        damagedShown = true;
        // Slumped and drained of colour, so the state reads at a glance in greybox.
        var faded = Color.Lerp(baseColour, new Color(0.30f, 0.30f, 0.33f), 0.45f);
        sr.sprite = GreyboxArt.IsoBox(tiles, Mathf.RoundToInt(fullHeightPx * 0.72f), faded, ppu);
    }

    void Collapse()
    {
        hp = 0f;
        AudioEvents.Play(Sfx.BuildingDestroyed, transform.position);

        if (bar != null) Destroy(bar.gameObject);

        var rubble = new Color(0.20f, 0.20f, 0.23f);
        sr.sprite = GreyboxArt.IsoBox(tiles, Mathf.RoundToInt(GreyboxArt.TileH * tiles * 0.22f), rubble, ppu);

        // Rubble is walkable, so the footprint goes away entirely.
        if (footprint != null) footprint.enabled = false;

        int drops = tiles >= 2 ? tuning.foodDropsLarge : tuning.foodDropsSmall;
        Food.Scatter(tuning, transform.position, drops, tiles >= 2, ppu);
    }
}
