using UnityEngine;

/// <summary>
/// The growth system — the thing this demo exists to answer.
///
/// Size moves continuously as the meter fills rather than only at tier-ups. Three
/// discrete jumps across a three-minute run leaves most of that run reading as
/// nothing happening; a curve with punctuation reads as constant progress. The
/// tier-up still lands as a moment: a jump in size, a full heal, a shake.
/// </summary>
public class PlayerProgress : MonoBehaviour
{
    public static PlayerProgress Instance { get; private set; }

    public Tuning tuning;

    /// <summary>0-based. Tier 0 is "size 1" in design terms.</summary>
    public int Tier { get; private set; }

    public float FoodThisTier { get; private set; }
    public float FoodTotal { get; private set; }
    public float Hp { get; private set; }

    public int MaxTier => Mathf.Max(0, tuning.tierScale.Length - 1);
    public bool AtMaxTier => Tier >= MaxTier;

    public float MaxHp => At(tuning.tierMaxHp, Tier, 100f);
    public float FoodForNextTier => AtMaxTier ? 0f : At(tuning.foodPerTier, Tier, 100f);
    public float TierProgress => AtMaxTier ? 1f : Mathf.Clamp01(FoodThisTier / Mathf.Max(1f, FoodForNextTier));

    /// <summary>Displayed size, 1 through 4, for anything user facing.</summary>
    public int SizeNumber => Tier + 1;

    public float DamageMultiplier => Mathf.Pow(tuning.damagePerTier, Tier);
    public float SpeedMultiplier => Mathf.Pow(tuning.speedPerTier, Tier);
    public float InfluenceRadius => tuning.foodInfluenceRadius * currentScale;

    float currentScale = 1f;
    PlayerController controller;
    CameraRig rig;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<PlayerController>();
    }

    void Start()
    {
        Hp = MaxHp;
        currentScale = TargetScale();
        controller.SetScale(currentScale);
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        // Framerate-independent ease toward the size the meter is currently asking for.
        currentScale = Mathf.Lerp(currentScale, TargetScale(),
                                  1f - Mathf.Exp(-tuning.growthLerpSpeed * Time.deltaTime));
        controller.SetScale(currentScale);
    }

    /// <summary>
    /// Where the body should be right now: the tier's base size, plus a fraction of
    /// the way toward the next tier's size, scaled by how full the meter is.
    /// </summary>
    float TargetScale()
    {
        float baseScale = At(tuning.tierScale, Tier, 1f);
        if (AtMaxTier) return baseScale;

        float nextScale = At(tuning.tierScale, Tier + 1, baseScale);
        return baseScale + (nextScale - baseScale) * tuning.withinTierGrowth * TierProgress;
    }

    public void AddFood(float amount)
    {
        FoodTotal += amount;
        if (AtMaxTier) return;

        FoodThisTier += amount;
        while (!AtMaxTier && FoodThisTier >= FoodForNextTier)
        {
            FoodThisTier -= FoodForNextTier;
            GrowTier();
        }
        if (AtMaxTier) FoodThisTier = 0f;
    }

    public void GrowTier()
    {
        if (AtMaxTier) return;

        Tier++;
        Hp = MaxHp;                     // reaching a size heals you into it
        AudioEvents.Play(Sfx.GrowTier, transform.position);
        Shake(tuning.tierUpShake);
    }

    /// <summary>Stage 4 calls this on death. Heals to the lower cap so it is a setback, not a spiral.</summary>
    public void ShrinkTier()
    {
        if (Tier <= 0) return;

        Tier--;
        FoodThisTier = 0f;
        Hp = MaxHp;
        AudioEvents.Play(Sfx.Shrink, transform.position);
        Shake(tuning.tierUpShake);
    }

    void Shake(float amount)
    {
        if (rig == null && Camera.main != null) rig = Camera.main.GetComponent<CameraRig>();
        if (rig != null) rig.Shake(amount, 0.35f);
    }

    static float At(float[] values, int i, float fallback) =>
        values != null && i >= 0 && i < values.Length ? values[i] : fallback;
}
