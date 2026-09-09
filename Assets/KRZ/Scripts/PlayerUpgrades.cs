using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player has collected, and the multipliers that fall out of it.
///
/// Everything compounds multiplicatively on top of the size-scaled base, so an
/// upgrade is worth the same proportion at size 5 as at size 1. Two of the six —
/// Stomp and Claws — change how you play rather than only how hard you hit, which
/// is what stops the set reading as six flavours of the same number.
/// </summary>
public class PlayerUpgrades : MonoBehaviour
{
    public static PlayerUpgrades Instance { get; private set; }

    public Tuning tuning;

    readonly Dictionary<UpgradeId, int> stacks = new();

    void Awake() => Instance = this;

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    public int Count(UpgradeId id) => stacks.TryGetValue(id, out int n) ? n : 0;
    public IReadOnlyDictionary<UpgradeId, int> Stacks => stacks;

    public bool HasStomp => Count(UpgradeId.Stomp) > 0;

    public float SwipeCooldownMul => Mul(UpgradeId.Brawler);
    public float SwipeRangeMul => Mul(UpgradeId.Claws);
    public float MoveSpeedMul => Mul(UpgradeId.Fleet);
    public float BlastCooldownMul => Mul(UpgradeId.Furnace);
    public float BlastPowerMul => Mul(UpgradeId.Beam);
    public float StompPowerMul => Mul(UpgradeId.Stomp);

    /// <summary>perStack compounded by how many are held, so stacking is smooth.</summary>
    float Mul(UpgradeId id)
    {
        var type = Find(id);
        if (type == null) return 1f;
        return Mathf.Pow(type.perStack, Count(id));
    }

    public UpgradeType Find(UpgradeId id)
    {
        if (tuning.upgrades == null) return null;
        foreach (var u in tuning.upgrades)
            if (u.id == id) return u;
        return null;
    }

    public bool IsMaxed(UpgradeId id)
    {
        var type = Find(id);
        return type != null && Count(id) >= type.maxStacks;
    }

    public void Grant(UpgradeId id)
    {
        var type = Find(id);
        if (type == null || IsMaxed(id)) return;

        stacks[id] = Count(id) + 1;
        AudioEvents.Play(Sfx.UpgradePickup, transform.position);
        Popups.Add(transform.position + Vector3.up * 1.5f,
                   $"<b>{type.displayName}</b>", type.colour);
    }

    /// <summary>Weighted pick, skipping anything already at its cap.</summary>
    public UpgradeType RollDrop()
    {
        if (tuning.upgrades == null || tuning.upgrades.Length == 0) return null;

        float total = 0f;
        foreach (var u in tuning.upgrades)
            if (!IsMaxed(u.id)) total += Mathf.Max(0f, u.weight);

        if (total <= 0f) return null;

        float roll = Random.value * total;
        foreach (var u in tuning.upgrades)
        {
            if (IsMaxed(u.id)) continue;
            roll -= Mathf.Max(0f, u.weight);
            if (roll <= 0f) return u;
        }
        return null;
    }
}
