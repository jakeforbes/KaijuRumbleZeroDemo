using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// What the player has collected, and the multipliers that fall out of it.
///
/// Everything compounds multiplicatively on top of the size-scaled base, so an
/// upgrade is worth the same proportion at size 5 as at size 1. Two of the six —
/// Stomp and Prism — change how you play rather than only how hard you hit, which
/// is what stops the set reading as six flavours of the same number.
/// </summary>
[SoundActions(Sfx.UpgradePickup)]
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

    /// <summary>Extra swipes per activation. One Brawler makes each attack a double tap.</summary>
    public int SwipeExtraHits
    {
        get
        {
            var type = Find(UpgradeId.Brawler);
            return type == null ? 0 : Count(UpgradeId.Brawler) * type.extraHitsPerStack;
        }
    }

    /// <summary>
    /// Beams the Blast fires, spaced evenly around the kaiju: 1, then 2, 4, 8.
    /// Doubling per stack is what makes each pickup read instantly — forward only,
    /// then front and back, then a cross, then a star. A percentage on an existing
    /// beam would have been invisible by comparison.
    /// </summary>
    public int BlastBeams => 1 << Mathf.Clamp(Count(UpgradeId.Prism), 0, 3);
    public float MoveSpeedMul => Mul(UpgradeId.Speed);
    public float BlastCooldownMul => Mul(UpgradeId.Furnace);
    public float BlastPowerMul => Mul(UpgradeId.Beam);
    /// <summary>
    /// Stomp is the one upgrade that creates an ability rather than modifying one,
    /// so its first stack grants it at base power and only later stacks multiply.
    /// Otherwise picking it up once already lands a boosted hit.
    /// </summary>
    public float StompPowerMul
    {
        get
        {
            int n = Count(UpgradeId.Stomp);
            if (n <= 0) return 0f;
            var type = Find(UpgradeId.Stomp);
            return type == null ? 1f : Mathf.Pow(type.perStack, n - 1);
        }
    }

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
        AudioEvents.Play(Sfx.UpgradePickup, transform.position, owner: gameObject);
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
