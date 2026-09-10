using UnityEngine;

/// <summary>How an enemy moves relative to the kaiju.</summary>
public enum MovementMode
{
    /// <summary>Walks at the player and stops at its attack range.</summary>
    Chase,

    /// <summary>Drifts chaotically and bolts when the player closes in.</summary>
    Flee,
}

/// <summary>Periodic behaviours an enemy can have on top of its basic attack.</summary>
public enum SpecialAction
{
    None,
    MissileVolley,
    DeployTroops,

    /// <summary>
    /// Hurls the player away, and into whatever is behind them. The Abomination's roar
    /// and a heavy mech's punch are the same action with different numbers.
    /// </summary>
    Knockback,
}

/// <summary>
/// One enemy species. sizeClass drives squishing: once your size exceeds it, you
/// kill this thing by walking over it. That threshold is the payoff for growing —
/// the enemy that was a threat two minutes ago becoming scenery.
/// </summary>
[System.Serializable]
public class EnemyType
{
    [Tooltip("Optional sound template override. Otherwise uses this unit's template in Resources/Enemy Sounds, then the shared Enemy Sounds template.")]
    public SoundPlayer sounds;

    public string name = "Grunt";
    [Tooltip("Controls boss music. The existing Abomination is also recognized automatically.")]
    public bool isBoss;

    [Tooltip("Squishable once the kaiju's size is above this. 0 is size 1.")]
    [Range(0, 4)] public int sizeClass;

    public float hp = 12f;

    [Tooltip("Flat damage subtracted from every hit taken. Makes auto-swipe feel " +
             "useless against heavies and gives the special a job.")]
    public float armour;

    public float contactDamage = 6f;
    public float moveSpeed = 3.4f;

    [Tooltip("How close it gets before it stops and attacks.")]
    public float attackRange = 0.9f;

    [Tooltip("Fires a tracer from where it stands instead of lunging. Set this rather " +
             "than relying on attackRange, so retuning a range cannot silently turn a " +
             "ranged attacker into a melee one.")]
    public bool ranged;
    public float attackCooldown = 1.1f;

    [Tooltip("Telegraph before the hit lands. Damage arriving with no warning reads as " +
             "unfair and cannot be tuned, because you never see what hit you.")]
    public float attackWindup = 0.35f;

    public int foodDrops = 2;
    public float foodScatter = 1.2f;

    [Tooltip("Leaves a power-up on death. Elites only — it is the reason to fight one " +
             "rather than walk away from it.")]
    public bool dropsUpgrade;

    [Tooltip("Whether it attacks at all. A Dropship keeps its distance and deploys " +
             "instead of ever striking.")]
    public bool attacks = true;

    [Header("Movement")]
    public MovementMode movement = MovementMode.Chase;

    [Tooltip("Flee only: how fast the drifting heading wanders, in radians per second. " +
             "This is what makes it read as skittish rather than as a straight retreat.")]
    public float wanderRate = 3f;

    [Tooltip("Flee only: distance at which it starts running rather than milling about. " +
             "Inside this it commits harder the closer the kaiju gets.")]
    public float fleeRadius = 9f;

    [Header("Special action")]
    [Tooltip("A periodic behaviour that plants the enemy, telegraphs, then fires. One " +
             "mechanism for every special so each new one is data rather than code.")]
    public SpecialAction special = SpecialAction.None;

    public float specialCooldown = 5f;

    [Tooltip("How long it stands still first. This is the tell — long enough to see it " +
             "coming and break away or close the distance.")]
    public float specialWindup = 1f;

    [Tooltip("Will not trigger from further than this, so off-screen enemies stay quiet.")]
    public float specialRange = 18f;

    [Header("Special: knockback")]
    [Tooltip("Damage the shove itself deals. 0 makes it pure displacement, which is " +
             "what the Abomination's roar is — losing your position mid-fight is the " +
             "punishment. A mech's punch sets this high and its cooldown long.")]
    public float specialDamage;

    [Tooltip("How far the player is thrown, in world units. 0 falls back to " +
             "Tuning.knockbackDistance.")]
    public float specialKnockback;

    [Tooltip("Sound for the shove. Each unit's own Sound Player prefab can point this " +
             "at a different clip, so a roar and a punch never share one.")]
    public Sfx specialSound = Sfx.BossRoar;

    [Header("Special: missile volley")]
    public int volleyCount = 8;
    public float missileDamage = 12f;
    public float missileSpeed = 11f;

    [Tooltip("How hard a missile can change direction. Low turns wide and is dodgeable; " +
             "high tracks you around corners.")]
    public float missileTurn = 40f;

    public float missileLife = 4f;

    [Header("Special: deploy troops")]
    [Tooltip("Name of the enemy type to deploy, matched against Tuning.enemyTypes.")]
    public string deployType = "Grunt";

    public int deployCount = 4;

    [Tooltip("How far from the dropship they land.")]
    public float deploySpread = 2.5f;

    [Tooltip("Stops deploying once this many of its own troops are already alive, so " +
             "one ignored Dropship cannot flood the arena.")]
    public int deployMaxAlive = 24;

    [Tooltip("Collider width as a fraction of bodyPx. Greybox capsules fill their box, " +
             "but delivered art is a figure inside a square canvas and is far narrower, " +
             "so art types want a much smaller value. Err small: clipping reads better " +
             "than an invisible wall.")]
    [Range(0.1f, 1f)] public float footprintFraction = 0.8f;

    [Tooltip("Body height in pixels at 128 PPU. 64 is roughly half a size-1 kaiju. " +
             "With delivered art this is only the collider and shadow size — the " +
             "sprite's own scale comes from artDisplayPx.")]
    public int bodyPx = 64;

    [Tooltip("Rigidbody mass. 0 derives it from bodyPx, which is right for everything " +
             "the kaiju is meant to brush aside. The player's mass grows with the " +
             "square of size and reaches 900 at size 5, so anything that should stand " +
             "its ground against a full-grown kaiju has to say so here.")]
    public float mass;

    [Tooltip("Draws arcs of electricity across the body. Purely cosmetic.")]
    public bool electrified;

    [Tooltip("Walks through buildings instead of around them.\n\n" +
             "For anything too big to use the streets. The Abomination's collider is " +
             "two and a half units wide and a street is closer to one, so it cannot " +
             "physically fit between blocks — it would spawn and then stand still for " +
             "the rest of the run, which is not a boss.\n\n" +
             "Also the right answer thematically: a kaiju that size wades through a " +
             "city rather than taking the corners. Building collision is switched off " +
             "for it outright, and its obstacle avoidance with it.")]
    public bool ignoresBuildings;

    [Header("Delivered art (blank = greybox)")]
    [Tooltip("Resources folder holding the frames, e.g. \"Mech\".")]
    public string artFolder = "";

    [Tooltip("Filename stem, e.g. \"mech\" for mech_walk_se_0001.png.")]
    public string artPrefix = "";

    [Tooltip("On-screen height in pixels. Frames are authored at 512, so this scales " +
             "them down the same way the kaiju's canvas scale does.")]
    public int artDisplayPx = 192;

    [Tooltip("How this package names directions. Uries uses south/southeast, the Mech " +
             "s/se, the FlyingTank S/SE.")]
    public DirectionStyle artDirectionStyle = DirectionStyle.ShortLower;

    [Tooltip("Where the frames sit. Tokens: {root} {prefix} {clip} {dir} {frame}.\n" +
             "Mech:       {root}/{clip}/{prefix}_{clip}_{dir}_{frame}\n" +
             "FlyingTank: {root}/{dir}/{prefix}_{dir}_{clip}_{frame}")]
    public string artPathFormat = "{root}/{clip}/{prefix}_{clip}_{dir}_{frame}";

    [Tooltip("Clip folder/file names in order: idle, walk, attack, hit, death. " +
             "Packages disagree — one says walk, another says Move.")]
    public string[] artClipNames = { "idle", "walk", "attack", "hit", "destruction" };

    [Tooltip("Five rendered directions with the rest mirrored, or all eight rendered. " +
             "The FlyingTank ships all eight, so nothing is flipped.")]
    public bool artMirrored = true;

    [Tooltip("Source frame size in pixels. Display scale is artDisplayPx / this, so a " +
             "192px package and a 512px one can sit side by side.")]
    public int artFrameSize = 512;

    [Tooltip("Multiplied over the sprite. One model can serve several roles by being " +
             "recoloured — keep tints pale or the art turns to mud.")]
    public Color artTint = Color.white;

    [Tooltip("Digits in the frame number, and what the first frame is called. " +
             "Uries uses 2 digits from 00, the Mech 4 digits from 0001.")]
    public int artFrameDigits = 4;
    public int artFirstFrame = 1;

    public int idleFrames = 4;
    public int walkFrames = 8;
    public int attackFrames = 6;
    public int hitFrames = 3;
    public int deathFrames = 5;

    public Color colour = new Color(0.85f, 0.45f, 0.35f);
}
