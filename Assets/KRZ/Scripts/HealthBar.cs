using UnityEngine;

/// <summary>
/// A no-numbers progress bar floating above a damaged building.
///
/// It only appears once something has been hit. A bar over every building in a
/// dense city is noise, and an intact building has nothing to say — but a damaged
/// one is a decision the player is in the middle of making.
/// </summary>
public class HealthBar : MonoBehaviour
{
    static Sprite backSprite, fillSprite;

    SpriteRenderer fill;
    Transform fillT;

    public static HealthBar Attach(Transform parent, float width, float topY, float ppu)
    {
        if (backSprite == null)
        {
            backSprite = GreyboxArt.Solid(64, 10, new Color(0.04f, 0.05f, 0.07f, 0.85f), ppu);
            fillSprite = GreyboxArt.Solid(64, 10, Color.white, ppu, 0f);
        }

        var root = new GameObject("HealthBar");
        root.transform.SetParent(parent, false);

        var backGo = new GameObject("back");
        backGo.transform.SetParent(root.transform, false);
        var bsr = backGo.AddComponent<SpriteRenderer>();
        bsr.sprite = backSprite;
        bsr.sortingOrder = 3;
        // Sprite is 64px wide at this ppu, so scaling by width/that gives world units.
        float unit = 64f / ppu;
        backGo.transform.localScale = new Vector3(width / unit, 1f, 1f);

        var fillGo = new GameObject("fill");
        fillGo.transform.SetParent(root.transform, false);
        var fsr = fillGo.AddComponent<SpriteRenderer>();
        fsr.sprite = fillSprite;
        fsr.sortingOrder = 4;
        fillGo.transform.localPosition = new Vector3(-width * 0.5f, 0f, 0f);

        var bar = root.AddComponent<HealthBar>();
        bar.fill = fsr;
        bar.fillT = fillGo.transform;
        bar.width = width;
        bar.unit = unit;
        bar.Reposition(topY);
        bar.Set(1f);
        return bar;
    }

    float width;
    float unit;

    public void Reposition(float topY) =>
        transform.localPosition = new Vector3(0f, topY + 0.25f, 0f);

    public void Set(float fraction)
    {
        fraction = Mathf.Clamp01(fraction);
        fillT.localScale = new Vector3(fraction * width / unit, 0.7f, 1f);

        // Green through amber to red, so progress reads without reading the length.
        fill.color = fraction > 0.5f
            ? Color.Lerp(new Color(1f, 0.75f, 0.2f), new Color(0.45f, 0.9f, 0.4f), (fraction - 0.5f) * 2f)
            : Color.Lerp(new Color(0.95f, 0.28f, 0.25f), new Color(1f, 0.75f, 0.2f), fraction * 2f);
    }
}
