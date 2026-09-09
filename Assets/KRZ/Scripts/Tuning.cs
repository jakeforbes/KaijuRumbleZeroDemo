using UnityEngine;

/// <summary>
/// Every tunable number in the prototype. Lives as a single asset at
/// Assets/KRZ/Resources/Tuning.asset so it can be edited in the Inspector
/// while the game is running. The arena builder never overwrites it.
/// </summary>
[CreateAssetMenu(menuName = "KRZ/Tuning", fileName = "Tuning")]
public class Tuning : ScriptableObject
{
    [Header("Movement")]
    public float moveSpeed = 7f;
    public float acceleration = 70f;
    public float deceleration = 90f;

    [Tooltip("Vertical movement multiplier. 0.5 matches the 2:1 isometric projection. " +
             "1.0 makes it feel like a flat top-down game.")]
    [Range(0.2f, 1f)] public float isoSquash = 0.5f;

    [Header("Camera")]
    public float pixelsPerUnit = 128f;

    [Tooltip("Orthographic half-height. 4.21875 shows sprites 1:1 at 1080p with 128 PPU.")]
    public float baseOrthoSize = 4.21875f;

    [Tooltip("Seconds for the camera to catch up. This is the only source of trailing — " +
             "higher is looser and calmer, lower is tighter and busier.")]
    public float followLag = 0.28f;

    [Tooltip("Hard cap on how far from centre the player can ever get, as a fraction of " +
             "the half-screen. Smoothing alone lets a fast kaiju drift further the faster " +
             "it moves; this is the box it can never leave.")]
    [Range(0.05f, 0.5f)] public float maxPlayerOffset = 0.22f;

    [Tooltip("Camera zooms out by (player scale ^ this). 0.5 is square root, 0 is no zoom, " +
             "1.0 fully cancels the growth fantasy.")]
    [Range(0f, 1f)] public float zoomExponent = 0.5f;

    [Header("Arena")]
    public int blocksX = 8;
    public int blocksY = 8;

    [Tooltip("World units between block centres. Y should be about half of X to look square in iso.")]
    public float blockSpacingX = 7f;
    public float blockSpacingY = 3.5f;
    public int randomSeed = 1337;

    [Header("Growth")]
    [Tooltip("Food needed to leave each tier. One fewer entry than there are sizes. " +
             "Roughly geometric rather than arithmetic: income accelerates hard as you " +
             "grow — wider pickup, faster kills, whole building classes becoming trivial — " +
             "so a flat +50 per gate meant later tiers arrived faster than earlier ones.")]
    public float[] foodPerTier = { 75f, 200f, 500f, 1150f };

    [Tooltip("Size at the start of each tier. Add or remove entries to change how many " +
             "sizes exist — everything else derives from this array's length.")]
    public float[] tierScale = { 1f, 1.75f, 2.5f, 3.25f, 4f };

    [Tooltip("Health cap at each tier. Reaching a tier heals you to its cap.")]
    public float[] tierMaxHp = { 100f, 140f, 175f, 210f, 250f };

    [Tooltip("How much of the gap to the next size is covered continuously as the meter " +
             "fills, with the rest arriving as a jump at tier-up. 0 makes growth purely " +
             "stepped, which is what makes most of a run read as nothing happening.")]
    [Range(0f, 1f)] public float withinTierGrowth = 0.7f;

    [Tooltip("Damage multiplier compounded per tier. Lowered from 1.35 when a fifth size " +
             "was added, so four steps land on the same 2.45x total three steps did.")]
    public float damagePerTier = 1.25f;

    [Tooltip("Move speed multiplier compounded per tier. Growth should never feel slower. " +
             "Lowered from 1.08 for the same reason as damage.")]
    public float speedPerTier = 1.06f;

    [Tooltip("How fast the body catches up to its target size. Lower is more elastic.")]
    public float growthLerpSpeed = 7f;

    public float tierUpShake = 0.5f;

    [Header("Enemies")]
    public EnemyType[] enemyTypes =
    {
        new EnemyType { name = "Grunt", sizeClass = 0, hp = 12f,  armour = 0f,
                        contactDamage = 6f,  moveSpeed = 1.28f, attackRange = 0.9f,
                        attackCooldown = 1.1f, foodDrops = 2, foodScatter = 1.2f,
                        bodyPx = 64,  colour = new Color(0.88f, 0.42f, 0.34f) },

        new EnemyType { name = "Tank",  sizeClass = 1, hp = 60f,  armour = 6f,
                        contactDamage = 18f, moveSpeed = 1.8f, attackRange = 4.5f, ranged = true,
                        attackCooldown = 2.2f, foodDrops = 5, foodScatter = 2f,
                        bodyPx = 96,  colour = new Color(0.80f, 0.60f, 0.25f) },

        new EnemyType { name = "Mech",  sizeClass = 2, hp = 140f, armour = 12f,
                        contactDamage = 26f, moveSpeed = 2.6f, attackRange = 1.6f,
                        attackCooldown = 1.6f, foodDrops = 9, foodScatter = 3f, volley = true,
                        bodyPx = 192, colour = new Color(0.72f, 0.35f, 0.55f) },

        // Elite grunt. Same silhouette and speed, ten times the health, double the
        // damage, and it leaves a power-up — the thing in a swarm worth stopping for.
        // Ranged, so it stays dangerous even though you outrun it five to one.
        new EnemyType { name = "Commander", sizeClass = 1, hp = 120f, armour = 0f,
                        contactDamage = 12f, moveSpeed = 1.28f, attackRange = 4.5f, ranged = true,
                        attackCooldown = 1.8f, attackWindup = 0.5f,
                        foodDrops = 6, foodScatter = 2.5f, dropsUpgrade = true,
                        bodyPx = 64, colour = new Color(0.65f, 0.35f, 0.95f) },

        // The boss. sizeClass 4 puts it beyond every squish threshold, so it is the
        // one thing in the game you can never walk over. Armour is set so the swipe
        // still contributes but the Blast is what actually fells it.
        new EnemyType { name = "Abomination", sizeClass = 4, hp = 1200f, armour = 12f,
                        contactDamage = 45f, moveSpeed = 2.2f, attackRange = 3f,
                        ranged = true, attackCooldown = 2.5f, attackWindup = 0.8f,
                        foodDrops = 0, foodScatter = 4f,
                        bodyPx = 560, colour = new Color(0.45f, 0.85f, 0.40f) },
    };

    [Tooltip("One Commander per this many Grunts, rolled per spawn within the range.")]
    public int commanderPerMin = 25;
    public int commanderPerMax = 50;

    [Header("Survival")]
    [Tooltip("Grace after any hit. Without it a swarm deletes you in a single frame.")]
    public float hitInvulnerability = 0.5f;

    [Tooltip("Grace after shrinking a tier, so a death is a setback rather than a spiral.")]
    public float shrinkInvulnerability = 1.5f;

    [Tooltip("How far the shrink shockwave reaches, scaled by your size.")]
    public float shockwaveRadius = 5f;
    public float shockwaveForce = 16f;

    [Tooltip("How close an outgrown enemy has to be to die underfoot, scaled by size.")]
    public float squishRange = 0.85f;

    [Tooltip("Smallest size that can squish anything, as a tier index. 2 means size 3.")]
    [Range(0, 4)] public int squishFirstTier = 2;

    [Tooltip("Extra sizes needed per enemy class. 2 means every other size unlocks the " +
             "next class up: smalls at size 3, mediums at size 5, larges never.")]
    [Range(1, 4)] public int squishTiersPerClass = 2;

    [Tooltip("How close a missile must get to the kaiju to hit, scaled by size.")]
    public float missileHitRadius = 0.9f;

    public float hitShake = 0.18f;

    [Tooltip("Floating damage numbers. The only way to tune damage by eye.")]
    public bool showDamageNumbers = true;

    [Header("Footprints")]
    [Tooltip("Building collision diamond as a fraction of its drawn base. " +
             "1.0 matches the art exactly. Below 1.0 lets the player creep onto the base.")]
    [Range(0.5f, 1.2f)] public float buildingFootprint = 1f;

    [Tooltip("Player collision ellipse in world units, at size 1. Tile is 2 x 1.")]
    public Vector2 playerFootprint = new Vector2(1.1f, 0.55f);

    [Header("Swipe — the auto attack")]
    public float swipeDamage = 10f;
    public float swipeCooldown = 2f;

    [Tooltip("Reach in world units at size 1, measured from the kaiju's edge outward. " +
             "Deliberately short: melee should mean getting close, with reach coming " +
             "from upgrades. Scales with size so the animation reaches what it hits.")]
    public float swipeRange = 1.3f;

    [Tooltip("Width of the hit arc in degrees, centred on facing.")]
    [Range(30f, 360f)] public float swipeArc = 130f;

    [Tooltip("Ceiling on the cone once Claws has widened it. 180 is a half circle — " +
             "everything in front of the kaiju.")]
    [Range(30f, 360f)] public float swipeArcMax = 180f;

    [Tooltip("Gap between the hits of a multi-hit swipe. Short enough to read as one " +
             "flurry, long enough that each hit is visible.")]
    public float swipeBurstInterval = 0.13f;

    [Tooltip("Draw the swipe arc briefly. A tuning aid, replaced by real VFX in Stage 9.")]
    public bool showSwipeArc = true;

    [Header("Blast — the manual special")]
    [Tooltip("One big number rather than chip damage: this is the answer to armour.")]
    public float blastDamage = 45f;
    public float blastCooldown = 6f;
    public float blastRange = 14f;

    [Tooltip("Full width of the beam in world units. A ground tile is 2 wide, so 1.0 " +
             "is half a tile.")]
    public float blastWidth = 1f;

    [Tooltip("Height the beam is drawn from, in world units at size 1, scaling with the " +
             "kaiju. The body is 1.0 tall, so 0.8 is about mouth height. Visual only — " +
             "hits stay on the ground plane where the footprints are.")]
    public float blastOriginHeight = 0.8f;

    [Header("Stomp — granted by the upgrade")]
    [Tooltip("Damage at one stack, deliberately half of blastDamage. Extra stacks " +
             "multiply from here rather than from a boosted first stack.")]
    public float stompDamage = 22.5f;
    public float stompCooldown = 4f;

    [Tooltip("Radius in world units at size 1. A ground tile is 2 wide, so 3.0 reaches " +
             "about a tile and a half around the kaiju.")]
    public float stompRadius = 3f;

    [Tooltip("How the radius grows with size: radius x (scale ^ this). 1.0 quadruples " +
             "it by size 5, which is sixteen times the area. 0.5 doubles it instead.")]
    [Range(0f, 1f)] public float stompRadiusExponent = 0.5f;

    [Tooltip("Stomp's damage against buildings only. It fires automatically with no " +
             "aiming, so at full strength it demolishes whatever you happen to stand " +
             "near and removes the choice of what to smash. Enemies still take full.")]
    [Range(0f, 1f)] public float stompBuildingMultiplier = 0.35f;

    [Header("Upgrades")]
    public UpgradeType[] upgrades =
    {
        // Brawler changes the rhythm: one extra strike per activation, per stack.
        // perStack stays 1.0 so it does not also shorten the cooldown — stacking a
        // rate cut with extra hits multiplies DPS far faster than either alone.
        new UpgradeType { id = UpgradeId.Brawler, displayName = "Brawler",
                          effect = "One extra strike per attack",
                          perStack = 1f, extraHitsPerStack = 1, maxStacks = 4,
                          weight = 1f, colour = new Color(1f, 0.55f, 0.35f) },

        // Claws changes the shape: longer and wider, up to a 180 degree half circle.
        new UpgradeType { id = UpgradeId.Claws,   displayName = "Claws",
                          effect = "Swipe reaches 25% further and 12 degrees wider",
                          perStack = 1.25f, arcPerStack = 12.5f, maxStacks = 4,
                          weight = 1f, colour = new Color(0.95f, 0.85f, 0.45f) },

        new UpgradeType { id = UpgradeId.Fleet,   displayName = "Fleet",
                          effect = "Move 12% faster", perStack = 1.12f, maxStacks = 5,
                          weight = 1f, colour = new Color(0.5f, 0.9f, 0.75f) },

        new UpgradeType { id = UpgradeId.Stomp,   displayName = "Stomp",
                          effect = "Shockwave around you every few seconds", perStack = 1.4f,
                          maxStacks = 4, weight = 0.9f, colour = new Color(1f, 0.8f, 0.35f) },

        new UpgradeType { id = UpgradeId.Beam,    displayName = "Beam",
                          effect = "Blast hits 30% harder and further", perStack = 1.3f,
                          maxStacks = 4, weight = 0.9f, colour = new Color(0.5f, 0.85f, 1f) },

        new UpgradeType { id = UpgradeId.Furnace, displayName = "Furnace",
                          effect = "Blast recharges 18% faster", perStack = 0.82f, maxStacks = 4,
                          weight = 0.9f, colour = new Color(0.75f, 0.6f, 1f) },
    };

    [Tooltip("Fallback chance for any non-Lab building to drop an upgrade. 0 now that " +
             "Labs exist — raise it to make the whole city pay out while testing.")]
    [Range(0f, 1f)] public float upgradeDropChance = 0f;

    [Tooltip("How close you must walk to collect one. Upgrades are not vacuumed.")]
    public float upgradePickupRange = 1.2f;

    [Header("Buildings")]
    [Tooltip("Five classes, one per kaiju size. HP is set so a matching size needs " +
             "about three swipes; the delta table below does the rest.")]
    public BuildingType[] buildingTypes =
    {
        new BuildingType { name = "Shack",    sizeClass = 0, tilesX = 1, tilesY = 1,
                           minHeightPx = 120, maxHeightPx = 180, hp = 30f,
                           foodDrops = 6,  foodScatter = 2.5f, weight = 30f,
                           colour = new Color(0.26f, 0.29f, 0.38f) },

        new BuildingType { name = "Row",      sizeClass = 1, tilesX = 2, tilesY = 1,
                           minHeightPx = 150, maxHeightPx = 230, hp = 38f,
                           foodDrops = 9,  foodScatter = 3.2f, weight = 25f,
                           colour = new Color(0.22f, 0.31f, 0.39f) },

        new BuildingType { name = "Wide Low", sizeClass = 2, tilesX = 2, tilesY = 2,
                           minHeightPx = 190, maxHeightPx = 260, hp = 47f,
                           foodDrops = 14, foodScatter = 4.2f, weight = 20f,
                           colour = new Color(0.29f, 0.27f, 0.37f) },

        new BuildingType { name = "Block",    sizeClass = 3, tilesX = 2, tilesY = 2,
                           minHeightPx = 380, maxHeightPx = 520, hp = 59f,
                           foodDrops = 22, foodScatter = 5.2f, weight = 15f,
                           colour = new Color(0.31f, 0.30f, 0.35f) },

        new BuildingType { name = "Tower",    sizeClass = 4, tilesX = 1, tilesY = 3,
                           minHeightPx = 620, maxHeightPx = 820, hp = 73f,
                           foodDrops = 34, foodScatter = 7f,  weight = 10f,
                           colour = new Color(0.25f, 0.26f, 0.42f) },

        // Laboratories are the only buildings that pay out power-ups, so they have to
        // read as prizes across a crowded street. Deliberately squat and a hue no
        // filler block uses — silhouette and colour are all greybox has to work with.
        new BuildingType { name = "Lab Small", sizeClass = 1, tilesX = 1, tilesY = 2,
                           minHeightPx = 200, maxHeightPx = 250, hp = 38f,
                           foodDrops = 8, foodScatter = 3f, upgradeDrops = 1, weight = 7f,
                           colour = new Color(0.20f, 0.62f, 0.60f) },

        new BuildingType { name = "Lab Large", sizeClass = 3, tilesX = 2, tilesY = 2,
                           minHeightPx = 260, maxHeightPx = 330, hp = 59f,
                           foodDrops = 18, foodScatter = 4.5f, upgradeDrops = 2, weight = 5f,
                           colour = new Color(0.24f, 0.72f, 0.68f) },
    };

    [Tooltip("Damage multiplier by (your size - the building's class), from -4 to +4. " +
             "The middle entry is your own class and is always 1. Left of it is the wall: " +
             "each step up in class roughly triples the work. Right of it is the payoff.")]
    public float[] damageVsBuildingByDelta = { 0.025f, 0.05f, 0.125f, 0.33f, 1f, 2f, 3f, 4f, 5f };

    [Tooltip("Progress bar over buildings you have damaged. Intact ones show nothing.")]
    public bool showBuildingHealthBars = true;

    [Header("Food")]
    public float foodValueSmall = 1f;
    public float foodValueMedium = 5f;
    public float foodValueLarge = 20f;

    [Tooltip("Outer edge of the pull field at size 1. Food further out is untouched.")]
    public float foodInfluenceRadius = 6.5f;

    [Tooltip("How the pull radius grows with size: radius x (scale ^ this). 1.0 is linear " +
             "and quadruples the radius by size 5 — sixteen times the collection area, " +
             "which is most of why the level curve ran away. 0.5 doubles it instead.")]
    [Range(0f, 1f)] public float foodRadiusExponent = 0.5f;

    [Tooltip("How sharply the pull falls off toward the edge. 1 is linear; higher keeps the " +
             "outer field nearly dead while ramping hard close to the body.")]
    [Range(1f, 6f)] public float foodPullFalloff = 4f;

    [Tooltip("Speed at the moment food reaches you — the top of the curve.")]
    public float foodMagnetMaxSpeed = 20f;

    [Tooltip("How quickly a piece takes up the speed the field is asking for. " +
             "Only bites near the body, where the field asks for a lot at once.")]
    public float foodMagnetAccel = 30f;

    [Tooltip("Seconds food spends arcing out before it settles.")]
    [Range(0.15f, 2f)] public float foodHopTime = 0.7f;

    [Tooltip("Seconds food sits before it will fly to you. Without this you never see it, " +
             "because you are standing next to the building you just smashed.")]
    public float foodArmDelay = 0.7f;

    [Header("Occluder fade")]
    [Tooltip("Fade buildings that are drawn in front of the player and covering them.")]
    public bool occluderFadeEnabled = true;

    [Tooltip("How transparent a covering building becomes. Lower is more see-through.")]
    [Range(0.1f, 1f)] public float occluderAlpha = 0.40f;

    [Tooltip("Seconds to fade in and out. Too fast reintroduces the pop, too slow smears.")]
    [Range(0.02f, 0.6f)] public float occluderFadeTime = 0.12f;

    [Header("Debug")]
    public bool showDebugHud = true;
    public bool enableCheatKeys = true;
    public bool showColliders = false;
}
