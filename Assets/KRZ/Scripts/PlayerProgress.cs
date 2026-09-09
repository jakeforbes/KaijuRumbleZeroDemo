using UnityEngine;

/// <summary>
/// Tracks food eaten. Stage 3 turns this into the growth system; for now it
/// accumulates and drives the meter so the feedback is on screen from the first
/// playtest rather than bolted on later.
/// </summary>
public class PlayerProgress : MonoBehaviour
{
    public static PlayerProgress Instance { get; private set; }

    public Tuning tuning;

    /// <summary>Food banked toward the next size, and the total across the run.</summary>
    public float FoodThisTier { get; private set; }
    public float FoodTotal { get; private set; }

    /// <summary>Placeholder until Stage 3 owns tiers. Keeps the meter honest meanwhile.</summary>
    public float FoodForNextTier => 100f;
    public float TierProgress => Mathf.Clamp01(FoodThisTier / FoodForNextTier);

    public float PickupRadius => tuning.pickupRadius * (controller != null ? controller.Scale : 1f);

    PlayerController controller;

    void Awake()
    {
        Instance = this;
        controller = GetComponent<PlayerController>();
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public void AddFood(float amount)
    {
        FoodThisTier += amount;
        FoodTotal += amount;
    }
}
