using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The Grubling upgrade: a small pet that trails the kaiju and charges what it finds.
///
/// The only upgrade that puts something on the field which can be out of position. Swarm and
/// Toxin follow you perfectly and always fire where you are; a grub can be across the street
/// finishing a Trooper when the thing you actually want dead walks up behind you. That gap
/// between what it is doing and what you want is the cost that pays for its damage, and it is
/// why the grub commits to a target for three seconds rather than re-picking every frame — a
/// pet that always chose optimally would be a homing missile with legs.
///
/// It never dashes. Dashing away leaves the grubs behind and they run to catch up, which is
/// the readable tell that they are followers rather than an aura pinned to your feet.
/// </summary>
public class Grubling : MonoBehaviour
{
    static Transform root;
    static readonly List<Grubling> all = new();
    static Sprite sprite;

    public static void Reset()
    {
        root = null;
        all.Clear();
        sprite = null;
    }

    Tuning tuning;
    Transform owner;
    SpriteRenderer art;

    /// <summary>Which of the pack this is, so they fan around the follow ring rather than stack.</summary>
    int slot;

    Enemy target;
    float nextChargeAt;
    float breakOffAt;
    float nextBiteAt;

    /// <summary>
    /// Brings the pack up or down to the number the upgrade grants. Called every frame from
    /// PlayerSpecial, which already drives the rest of the upgrade-granted abilities — a grub
    /// is spawned the frame its stack lands rather than waiting for anything to tick.
    /// </summary>
    public static void Sync(Transform owner, int wanted, Tuning tuning)
    {
        // Destroyed grubs leave nulls behind: a scene reload takes the objects without ever
        // telling this list, and counting those as living pets would starve the pack forever.
        all.RemoveAll(g => g == null);

        while (all.Count > wanted)
        {
            var last = all[all.Count - 1];
            all.RemoveAt(all.Count - 1);
            if (last != null) Destroy(last.gameObject);
        }

        while (all.Count < wanted) Spawn(owner, tuning);

        for (int i = 0; i < all.Count; i++) all[i].slot = i;
    }

    static void Spawn(Transform owner, Tuning tuning)
    {
        if (root == null) root = new GameObject("Grublings").transform;
        if (sprite == null)
            sprite = GreyboxArt.Capsule(Mathf.RoundToInt(tuning.grublingPx * 0.7f),
                                        tuning.grublingPx, Color.white, tuning.pixelsPerUnit);

        var go = new GameObject("grub");
        go.transform.SetParent(root, false);
        go.transform.position = owner.position;

        var sr = go.AddComponent<SpriteRenderer>();
        sr.sprite = sprite;
        sr.color = tuning.grublingColour;

        // Sorted from the ground contact point like every other body in the world, so a grub
        // walks behind the buildings and the kaiju it is standing behind.
        sr.spriteSortPoint = SpriteSortPoint.Pivot;

        var g = go.AddComponent<Grubling>();
        g.tuning = tuning;
        g.owner = owner;
        g.art = sr;
        g.nextChargeAt = Time.time + tuning.grublingInterval;

        all.Add(g);
    }

    void OnDestroy() => all.Remove(this);

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null || owner == null) { Destroy(gameObject); return; }
        if (progress.RunOver) return;

        float body = progress.Scale;
        art.transform.localScale = Vector3.one * Mathf.Max(0.3f, body * 0.45f);

        // Committed targets are dropped the moment they die or the three seconds are up, and
        // the cooldown starts from that moment — a grub that killed something early is back
        // at your heel and rearming rather than standing over the corpse.
        if (target != null && (!target.IsAlive || Time.time >= breakOffAt)) Release();

        if (target == null && Time.time >= nextChargeAt) target = FindTarget(body);

        if (target != null) Attack(progress, body);
        else Follow(progress, body);
    }

    void Release()
    {
        target = null;
        nextChargeAt = Time.time + tuning.grublingInterval;
    }

    Enemy FindTarget(float body)
    {
        Enemy best = null;
        float bestSq = float.MaxValue;
        Vector2 at = transform.position;

        foreach (var e in Enemy.All)
        {
            if (e == null || !e.IsAlive) continue;

            Vector2 d = (Vector2)e.transform.position - at;
            var flat = new Vector2(d.x, d.y / tuning.isoSquash);
            float sq = flat.sqrMagnitude;

            if (sq > tuning.grublingRange * tuning.grublingRange || sq >= bestSq) continue;
            bestSq = sq;
            best = e;
        }

        if (best != null) breakOffAt = Time.time + tuning.grublingAttackSeconds;
        return best;
    }

    void Attack(PlayerProgress progress, float body)
    {
        Vector2 d = (Vector2)target.transform.position - (Vector2)transform.position;
        var flat = new Vector2(d.x, d.y / tuning.isoSquash);
        float reach = tuning.grublingBiteFraction * body;

        if (flat.magnitude > reach)
        {
            Move(flat.normalized, Speed(progress, tuning.grublingChargeSpeedPlayerMultiple));
            return;
        }

        if (Time.time < nextBiteAt) return;
        nextBiteAt = Time.time + tuning.grublingBiteInterval;

        // Enemies only. A pack of four chewing through the city on its own would take the
        // choice of what to smash away from the player, the same reason Trample skips
        // buildings and Stomp only chips them.
        target.TakeDamage(tuning.grublingDamage * progress.DamageMultiplier, transform.position);
        ShockwaveFx.Show(transform.position, tuning.grublingColour, 0.22f, 0.5f, 0.16f);
    }

    /// <summary>
    /// Trails the kaiju at a fixed ring, with the pack fanned around it. Deliberately a
    /// position to reach rather than a leash: a grub that has fallen behind runs flat out and
    /// one that is already home stands still, so the pack strings out behind a dash and
    /// gathers back in when you stop.
    /// </summary>
    void Follow(PlayerProgress progress, float body)
    {
        int count = Mathf.Max(1, all.Count);
        float angle = (slot + 0.5f) / count * Mathf.PI * 2f;
        float ring = tuning.grublingFollowFraction * body;

        Vector2 home = (Vector2)owner.position
                     + new Vector2(Mathf.Cos(angle) * ring, Mathf.Sin(angle) * ring * tuning.isoSquash);

        Vector2 d = home - (Vector2)transform.position;
        var flat = new Vector2(d.x, d.y / tuning.isoSquash);

        // A dead zone, or the pack jitters around its own target position forever.
        if (flat.magnitude < ring * 0.25f) return;

        Move(flat.normalized, Speed(progress, tuning.grublingSpeedPlayerMultiple));
    }

    /// <summary>
    /// A multiple of the kaiju's own top speed, matching how the bone is paced. A fixed figure
    /// that kept up at size 1 would be left behind for good once speedPerTier and the Speed
    /// upgrade had compounded.
    /// </summary>
    float Speed(PlayerProgress progress, float multiple)
    {
        var upgrades = PlayerUpgrades.Instance;
        float player = tuning.moveSpeed * progress.SpeedMultiplier
                     * (upgrades != null ? upgrades.MoveSpeedMul : 1f);
        return player * multiple;
    }

    void Move(Vector2 flatDir, float speed)
    {
        var screen = new Vector2(flatDir.x, flatDir.y * tuning.isoSquash);
        transform.position += (Vector3)(screen * (speed * Time.deltaTime));

        // Faces the way it is going, like every other body in the world.
        if (Mathf.Abs(flatDir.x) > 0.01f) art.flipX = flatDir.x < 0f;
    }
}
