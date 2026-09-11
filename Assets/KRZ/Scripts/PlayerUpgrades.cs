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
    public bool HasSwarm => Count(UpgradeId.Swarm) > 0;
    public bool HasToxin => Count(UpgradeId.Toxin) > 0;

    /// <summary>
    /// Particles per discharge: the base count on the first stack, one more for each
    /// after it. The count is the readable half of the upgrade — you can see three
    /// become four — while the damage multiplier is the half that keeps it relevant.
    /// </summary>
    public int SwarmBolts
    {
        get
        {
            int n = Count(UpgradeId.Swarm);
            if (n <= 0) return 0;
            var type = Find(UpgradeId.Swarm);
            if (type == null) return 0;
            return Mathf.Max(1, type.baseProjectiles + (n - 1) * type.extraProjectilesPerStack);
        }
    }

    public float SwarmPowerMul => GrantedPowerMul(UpgradeId.Swarm);
    public float ToxinPowerMul => GrantedPowerMul(UpgradeId.Toxin);

    public bool HasTrample => Count(UpgradeId.Trample) > 0;
    public float TramplePowerMul => GrantedPowerMul(UpgradeId.Trample);

    public bool HasSlam => Count(UpgradeId.Slam) > 0;
    public float SlamPowerMul => GrantedPowerMul(UpgradeId.Slam);

    /// <summary>
    /// Slam's reach, as a multiple of the swipe's. Exactly 1 at the first stack — the
    /// upgrade's promise is a swipe-ranged blow at triple damage — and growing from
    /// there, so later stacks widen the cone as well as deepening the hit.
    /// </summary>
    public float SlamRangeMul
    {
        get
        {
            int n = Count(UpgradeId.Slam);
            return n <= 0 ? 0f : Mathf.Pow(tuning.slamRangePerStack, n - 1);
        }
    }

    /// <summary>
    /// How long each puff of the cloud lingers: the base life plus a second for every
    /// stack after the first. Length is the readable half of Toxin the way particle
    /// count is for Swarm — you can see the wake stretch further behind you, where a
    /// damage multiplier on the same short cloud would have been invisible.
    ///
    /// Read at the moment a puff is emitted, so picking the upgrade up lengthens the
    /// trail from there on rather than retroactively extending gas already laid down.
    /// </summary>
    public float ToxinCloudLife
    {
        get
        {
            int n = Count(UpgradeId.Toxin);
            if (n <= 0) return tuning.toxinCloudLife;

            var type = Find(UpgradeId.Toxin);
            if (type == null) return tuning.toxinCloudLife;
            return tuning.toxinCloudLife + (n - 1) * type.extraSecondsPerStack;
        }
    }

    /// <summary>
    /// Stomp is the one upgrade that creates an ability rather than modifying one,
    /// so its first stack grants it at base power and only later stacks multiply.
    /// Otherwise picking it up once already lands a boosted hit.
    /// </summary>
    public float StompPowerMul => GrantedPowerMul(UpgradeId.Stomp);

    /// <summary>
    /// For upgrades that grant an ability rather than modify one. Returns 0 when the
    /// ability is not held at all, so callers can use it as both the switch and the
    /// scale, and the first stack is worth exactly its base numbers.
    /// </summary>
    float GrantedPowerMul(UpgradeId id)
    {
        int n = Count(id);
        if (n <= 0) return 0f;
        var type = Find(id);
        return type == null ? 1f : Mathf.Pow(type.perStack, n - 1);
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
