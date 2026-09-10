using UnityEngine;

/// <summary>
/// Every tunable number in the prototype. Lives as a single asset at
/// Assets/KRZ/Resources/Tuning.asset so it can be edited in the Inspector
/// while the game is running. The arena builder never overwrites it.
/// </summary>
[CreateAssetMenu(menuName = "KRZ/Tuning", fileName = "Tuning")]
public class Tuning : ScriptableObject
{
    [Header("Sound templates (Sound Player prefabs)")]
    public SoundPlayer playerSounds;
    public SoundPlayer enemySounds;
    public SoundPlayer buildingSounds;
    public SoundPlayer foodSounds;

    [Header("Movement")]
    public float moveSpeed = 2.1f;
    public float acceleration = 70f;
    public float deceleration = 90f;

    [Tooltip("Vertical movement multiplier. 0.5 matches the 2:1 isometric projection. " +
             "1.0 makes it feel like a flat top-down game.")]
    [Range(0.2f, 1f)] public float isoSquash = 0.5f;

    [Header("Camera")]
    [Min(0.01f)] public float bossIntroPanSeconds = 1.25f;
    [Min(0)] public float bossIntroHoldSeconds = 2f;
    [Min(0.01f)] public float bossIntroReturnSeconds = 1.25f;
    public float pixelsPerUnit = 128f;

    [Tooltip("Orthographic half-height. 4.21875 shows sprites 1:1 at 1080p with 128 PPU.")]
    public float baseOrthoSize = 4.21875f;

    [Tooltip("Seconds for the camera to catch up. This is the only source of trailing — " +
             "higher is looser and calmer, lower is tighter and busier.")]
    public float followLag = 0.34f;

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

    [Tooltip("How many blocks around the spawn point are left empty. 0 clears only " +
             "the block the player stands on, so the run opens with buildings on all " +
             "sides and food within reach. Raise it to open the start out again.")]
    [Range(0, 3)] public int startClearBlocks;

    [Header("Growth")]
    [Tooltip("Food needed to leave each tier. One fewer entry than there are sizes.\n\n" +
             "Each gate is roughly triple the last, because income does not just grow " +
             "with size, it compounds: a wider pickup field, faster kills, more upgrades, " +
             "and whole building classes turning from walls into food. A gate that only " +
             "doubles gets swallowed by that and the late sizes blur past.\n\n" +
             "Aim: size 5 arriving around 2:30, just before the boss at 2:40. If you are " +
             "hitting it far early, raise the last two entries — they are where the run " +
             "is decided.\n\n" +
             "Size and power are separate curves: if the run feels underpowered rather " +
             "than under-sized, that is power-up frequency (Lab weights), not these.")]
    public float[] foodPerTier = { 80f, 320f, 1100f, 3200f };

    [Tooltip("Size at the start of each tier. Add or remove entries to change how many " +
             "sizes exist — everything else derives from this array's length." +
             " Camera zoom is relative to the first entry, so raising every value makes " +
             "the kaiju read bigger on screen rather than just pulling the camera back.")]
    public float[] tierScale = { 1.5f, 2.625f, 3.75f, 4.875f, 6f };

    [Tooltip("Health cap at each tier. Reaching a tier heals you to its cap.")]
    public float[] tierMaxHp = { 100f, 150f, 205f, 265f, 330f };

    [Tooltip("Flat damage subtracted from every hit taken, by size — the kaiju's own " +
             "armour. Flat rather than a percentage so small-arms fire falls away " +
             "entirely as you grow while heavy attacks still land, which is what stops " +
             "losing a size being a routine event.")]
    public float[] damageResistBySize = { 0f, 2f, 4f, 7f, 10f };

    [Tooltip("Seconds of eased-off spawning after losing a size. The run clock keeps " +
             "going, so the boss still arrives on schedule.")]
    public float recoverySeconds = 14f;

    [Tooltip("Sustained wave intervals are multiplied by this while recovering. " +
             "2.5 means roughly 40% of the usual pressure.")]
    public float recoverySpawnInterval = 2.5f;

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
        // Grunt, Trooper, Commander and Dropship all share the FlyingTank model,
        // separated by size and tint. One delivery covering four roles was a
        // deliberate trade against the clock — they read apart because their
        // silhouette scale and colour differ, not because they are different art.
        new EnemyType { name = "Grunt", sizeClass = 0, hp = 12f,  armour = 0f,
                        contactDamage = 6f,  moveSpeed = 0.96f, attackRange = 0.9f,
                        attackCooldown = 1.1f, foodDrops = 2, foodScatter = 1.2f,
                        bodyPx = 64,  colour = new Color(0.88f, 0.42f, 0.34f),
                        artFolder = "FlyingTank", artPrefix = "FlyingTank",
                        artPathFormat = "{root}/{dir}/{prefix}_{dir}_{clip}_{frame}",
                        artClipNames = new[] { "Idle", "Move", "Attack", "Hit", "Destruction" },
                        artDirectionStyle = DirectionStyle.ShortUpper, artMirrored = false,
                        artFrameSize = 192, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 2, walkFrames = 6, attackFrames = 4,
                        hitFrames = 2, deathFrames = 4,
                        artDisplayPx = 84, footprintFraction = 0.4f,
                        artTint = new Color(1f, 0.72f, 0.62f) },

        // Trooper: the missing rung. Twice a Grunt's health and a short gun, but the
        // same class and speed — so it is still squishable at size 3 and still
        // outrun. It exists because Grunt to Tank was a cliff: 5x health, 3x damage
        // and armour all at once.
        new EnemyType { name = "Trooper", sizeClass = 0, hp = 24f, armour = 0f,
                        contactDamage = 10f, moveSpeed = 0.96f,
                        attackRange = 3f, ranged = true,
                        attackCooldown = 1.6f, attackWindup = 0.45f,
                        foodDrops = 3, foodScatter = 1.6f,
                        bodyPx = 68, colour = new Color(0.78f, 0.30f, 0.42f),
                        artFolder = "FlyingTank", artPrefix = "FlyingTank",
                        artPathFormat = "{root}/{dir}/{prefix}_{dir}_{clip}_{frame}",
                        artClipNames = new[] { "Idle", "Move", "Attack", "Hit", "Destruction" },
                        artDirectionStyle = DirectionStyle.ShortUpper, artMirrored = false,
                        artFrameSize = 192, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 2, walkFrames = 6, attackFrames = 4,
                        hitFrames = 2, deathFrames = 4,
                        artDisplayPx = 100, footprintFraction = 0.4f,
                        artTint = new Color(1f, 0.52f, 0.60f) },

        // Scavenger. Grunt-sized, three times the health, fast and skittish, and it
        // never fights back. It is a chase: catching one pays out a huge scattered
        // burst of food, so it is the one enemy you go out of your way to reach
        // rather than one you deal with because it reached you.
        new EnemyType { name = "Scavenger", sizeClass = 0, hp = 36f, armour = 0f,
                        contactDamage = 0f, attacks = false,
                        movement = MovementMode.Flee, wanderRate = 3.2f, fleeRadius = 10f,
                        moveSpeed = 1.85f, attackRange = 0f,
                        foodDrops = 26, foodScatter = 9f,
                        bodyPx = 64, colour = new Color(0.55f, 0.90f, 0.40f) },

        // The one enemy with its own ground vehicle art. Drawn a little under a
        // size-1 kaiju's height and half again as wide, so it reads as armour rather
        // than as a large soldier. Body and footprint went up with the silhouette,
        // but the footprint stays under the drawn width — clipping a track corner is
        // better than being stopped by air.
        new EnemyType { name = "Tank",  sizeClass = 1, hp = 60f,  armour = 6f,
                        contactDamage = 18f, moveSpeed = 1.35f, attackRange = 4.5f, ranged = true,
                        attackCooldown = 2.2f, foodDrops = 5, foodScatter = 2f,
                        bodyPx = 128, colour = new Color(0.80f, 0.60f, 0.25f),
                        artFolder = "GroundTank", artPrefix = "GroundTank",
                        artPathFormat = "{root}/{dir}/{clip}/{prefix}_{dir}_{clip}_{frame}",
                        artClipNames = new[] { "Idle", "Drive", "AttackBlast", "Hit", "Destruction" },
                        artDirectionStyle = DirectionStyle.ShortUpper, artMirrored = false,
                        artFrameSize = 512, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 4, walkFrames = 6, attackFrames = 4,
                        hitFrames = 3, deathFrames = 4,
                        artDisplayPx = 256, footprintFraction = 0.75f },

        // Elite Tank. Takes the Commander's job — the enemy worth stopping for —
        // from size 3 onward, because by then a Commander dies in passing and a prize
        // you collect without deciding to is not a prize.
        //
        // Class 2, so unlike the Commander it never becomes squishable. The power-up
        // dispenser should stay something you have to actually kill for the whole run,
        // rather than turning into something you absorb by walking through it.
        //
        // Electrified and gold so it is findable in a swarm. Everything about this
        // unit is "the one to shoot"; if it does not read at a glance it has failed
        // whatever its stats say.
        new EnemyType { name = "Elite Tank", sizeClass = 2, hp = 200f, armour = 14f,
                        contactDamage = 26f, moveSpeed = 1.5f, attackRange = 4.5f, ranged = true,
                        attackCooldown = 2f, attackWindup = 0.5f,
                        dropsUpgrade = true, foodDrops = 16, foodScatter = 3.5f,
                        bodyPx = 150, electrified = true,
                        colour = new Color(1f, 0.82f, 0.30f),
                        artFolder = "GroundTank", artPrefix = "GroundTank",
                        artPathFormat = "{root}/{dir}/{clip}/{prefix}_{dir}_{clip}_{frame}",
                        artClipNames = new[] { "Idle", "Drive", "AttackBlast", "Hit", "Destruction" },
                        artDirectionStyle = DirectionStyle.ShortUpper, artMirrored = false,
                        artFrameSize = 512, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 4, walkFrames = 6, attackFrames = 4,
                        hitFrames = 3, deathFrames = 4,
                        artDisplayPx = 310, footprintFraction = 0.75f,
                        artTint = new Color(1f, 0.84f, 0.42f) },

        new EnemyType { name = "Mech",  sizeClass = 2, hp = 140f, armour = 12f,
                        contactDamage = 26f, moveSpeed = 1.7f, attackRange = 1.6f,
                        attackCooldown = 1.6f, foodDrops = 9, foodScatter = 3f,
                        special = SpecialAction.MissileVolley,
                        bodyPx = 384, colour = new Color(0.72f, 0.35f, 0.55f),
                        artFolder = "Mech", artPrefix = "mech", artDisplayPx = 384, footprintFraction = 0.34f,
                        artFrameDigits = 4, artFirstFrame = 1 },

        // Bruiser. The same chassis a fifth larger, with the missiles taken away and a
        // punch put in — the one thing in the roster that closes on you deliberately
        // and wants to be in your face.
        //
        // Everything about it is the long telegraph. 1.5 seconds planted, a punch reach
        // barely longer than its own arm, and nine seconds before it can do it again.
        // Read it and you walk out of the ring for free; miss it and you lose a third
        // of your health and your position at once. That trade is the whole unit.
        //
        // Heavier than the player until size 4, so it shoulders you around rather than
        // being brushed aside like the rest of the roster.
        new EnemyType { name = "Bruiser", sizeClass = 2, hp = 190f, armour = 14f,
                        contactDamage = 30f, moveSpeed = 1.55f, attackRange = 1.8f,
                        attackCooldown = 1.8f, attackWindup = 0.5f,
                        foodDrops = 14, foodScatter = 3.5f,
                        special = SpecialAction.Knockback,
                        specialCooldown = 9f, specialWindup = 1.5f, specialRange = 4.5f,
                        specialDamage = 85f, specialKnockback = 7f,
                        specialSound = Sfx.MechPunch,
                        bodyPx = 470, mass = 420f, colour = new Color(0.95f, 0.50f, 0.28f),
                        artFolder = "Mech", artPrefix = "mech", artDisplayPx = 470,
                        footprintFraction = 0.34f, artFrameDigits = 4, artFirstFrame = 1,
                        artTint = new Color(1f, 0.62f, 0.42f) },

        // Dropship. A Tank hull that never fires: it holds station and unloads Grunts,
        // so it replaces the cut Barracks with something mobile and killable. Ignoring
        // it costs you the swarm rather than health, which makes it a priority target
        // by choice instead of by damage.
        new EnemyType { name = "Dropship", sizeClass = 1, hp = 85f, armour = 2f,
                        contactDamage = 0f, attacks = false,
                        moveSpeed = 1.8f, attackRange = 6f,
                        special = SpecialAction.DeployTroops,
                        specialCooldown = 6f, specialWindup = 1.2f, specialRange = 15f,
                        deployType = "Grunt", deployCount = 4, deploySpread = 2.5f,
                        foodDrops = 7, foodScatter = 2.5f,
                        bodyPx = 112, colour = new Color(0.45f, 0.75f, 0.85f),
                        artFolder = "FlyingTank", artPrefix = "FlyingTank",
                        artPathFormat = "{root}/{dir}/{prefix}_{dir}_{clip}_{frame}",
                        artClipNames = new[] { "Idle", "Move", "Attack", "Hit", "Destruction" },
                        artDirectionStyle = DirectionStyle.ShortUpper, artMirrored = false,
                        artFrameSize = 192, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 2, walkFrames = 6, attackFrames = 4,
                        hitFrames = 2, deathFrames = 4,
                        artDisplayPx = 210, footprintFraction = 0.34f,
                        artTint = new Color(0.62f, 0.88f, 1f) },

        // Elite grunt. Same silhouette and speed, ten times the health, double the
        // damage, and it leaves a power-up — the thing in a swarm worth stopping for.
        // Ranged, so it stays dangerous even though you outrun it five to one.
        new EnemyType { name = "Commander", sizeClass = 1, hp = 120f, armour = 0f,
                        contactDamage = 12f, moveSpeed = 0.96f, attackRange = 4.5f, ranged = true,
                        attackCooldown = 1.8f, attackWindup = 0.5f,
                        foodDrops = 6, foodScatter = 2.5f, dropsUpgrade = true,
                        bodyPx = 64, colour = new Color(0.65f, 0.35f, 0.95f),
                        artFolder = "FlyingTank", artPrefix = "FlyingTank",
                        artPathFormat = "{root}/{dir}/{prefix}_{dir}_{clip}_{frame}",
                        artClipNames = new[] { "Idle", "Move", "Attack", "Hit", "Destruction" },
                        artDirectionStyle = DirectionStyle.ShortUpper, artMirrored = false,
                        artFrameSize = 192, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 2, walkFrames = 6, attackFrames = 4,
                        hitFrames = 2, deathFrames = 4,
                        artDisplayPx = 104, footprintFraction = 0.4f,
                        artTint = new Color(0.78f, 0.55f, 1f) },

        // The boss. sizeClass 4 puts it beyond every squish threshold, so it is the
        // one thing in the game you can never walk over. Armour is set so the swipe
        // still contributes but the Blast is what actually fells it.
        // The boss, and the run's end condition. 2400 health is deliberately more
        // than the swipe alone can chew through in the time you have: it is the one
        // fight that asks you to have built something.
        //
        // The roar exists because everything else in the roster can be outrun, which
        // at size 5 turns a boss into a stationary target you circle. The knockback
        // takes your spacing away and hands it back on the boss's terms — and points
        // you at whatever building is behind you.
        // Built from the player's own size-5 frames, recoloured and a fifth larger.
        // A boss that is unmistakably your own kind, bigger, is a cheaper and clearer
        // read than any amount of new art would have been — and the size difference
        // does the talking the moment it walks on.
        //
        // artDisplayPx 922 is not arbitrary: a 512 frame at 128 PPU is 4 world units,
        // so 922/512 of that is 7.2 — exactly 20% over the player's 6 at size 5.
        //
        // Mass 900 matches a size-5 kaiju's own, so the two of them shove rather than
        // one bulldozing the other. Everything else in the roster is meant to be
        // brushed aside; this is the one thing that is not.
        //
        // The roar deals no damage on purpose. Losing your position and your footing
        // in the middle of the only fight that matters is the punishment, and the
        // building you land in takes the hit instead.
        new EnemyType { name = "Abomination", sizeClass = 4, hp = 2400f, armour = 12f,
                        contactDamage = 45f, moveSpeed = 1.65f, attackRange = 3f,
                        ranged = true, attackCooldown = 2.5f, attackWindup = 0.8f,
                        special = SpecialAction.Knockback,
                        specialCooldown = 7f, specialWindup = 1.1f, specialRange = 9f,
                        specialSound = Sfx.BossRoar,
                        foodDrops = 0, foodScatter = 4f,
                        bodyPx = 700, mass = 900f, electrified = true,
                        colour = new Color(0.45f, 0.85f, 0.40f),
                        artFolder = "Uries/Level_5", artPrefix = "uries_l5",
                        artPathFormat = "{root}/{clip}/{dir}/{prefix}_{clip}_{dir}_{frame}",
                        artClipNames = new[] { "idle", "walk", "swipe", "hit", "hit" },
                        artDirectionStyle = DirectionStyle.LongLower, artMirrored = true,
                        artFrameSize = 512, artFrameDigits = 2, artFirstFrame = 0,
                        idleFrames = 4, walkFrames = 8, attackFrames = 6,
                        hitFrames = 3, deathFrames = 3,
                        artDisplayPx = 922, footprintFraction = 0.45f,
                        artTint = new Color(0.45f, 1f, 0.55f) },
    };

    [Tooltip("One Commander per this many Grunts, rolled per spawn within the range.")]
    public int commanderPerMin = 25;
    public int commanderPerMax = 50;

    [Header("Who carries the power-ups")]
    [Tooltip("The enemy that leads a squad and leaves an upgrade, early on.")]
    public string upgradeCarrierType = "Commander";

    [Tooltip("Takes that job over once the player is big enough. A Commander stops " +
             "being a decision worth making the moment you can kill one in passing, " +
             "so the prize moves to something that still costs you a stop.")]
    public string eliteCarrierType = "Elite Tank";

    [Tooltip("Tier index at which the elite takes over, 0-based. 2 is size 3.")]
    public int eliteCarrierFromTier = 2;

    [Header("The run")]
    [Tooltip("Free power-ups placed around the map at the start, evenly spaced.")]
    public int freeUpgradeCount = 4;

    [Tooltip("How far from the start point they sit, in world units.")]
    public float freeUpgradeRadius = 18f;

    [Tooltip("Hard ceiling on living enemies. The timeline is written to push against " +
             "this rather than to stay under it, so the cap is what actually sets the " +
             "peak crowd — and protects the framerate.")]
    public int maxEnemiesAlive = 60;

    [Tooltip("The whole arc, as data. Times are seconds into the run. A beat with an " +
             "interval repeats until its end time; without one it fires once.")]
    public WaveEntry[] waves =
    {
        // First contact at 0:05: three squads of three, two seconds apart, each from a
        // different side. Small enough to be a lesson rather than a threat.
        new WaveEntry { label = "first contact", startTime = 5f, endTime = 9f, interval = 2f,
                        enemyType = "Grunt", count = 3, shape = SpawnShape.Clump },

        // A second, slightly bigger probe at 0:15, before the real line forms. Keeps
        // the opening minute moving rather than leaving a ten-second gap.
        new WaveEntry { label = "probe", startTime = 15f, endTime = 19f, interval = 2f,
                        enemyType = "Grunt", count = 4, shape = SpawnShape.Clump },

        // The real infantry line. A Commander leads the first squad and every third
        // after — a rhythm you can learn rather than a roll you cannot read.
        new WaveEntry { label = "infantry", startTime = 25f, endTime = 155f, interval = 10f,
                        enemyType = "Grunt", count = 9, shape = SpawnShape.Clump,
                        commanderEvery = 3,
                        veteranType = "Trooper", veteranFromFire = 2,
                        veteranStartFraction = 0.3f, veteranRampPerFire = 0.07f },

        // Scavengers turn up throughout. Two at a time so one getting away still
        // leaves a chase worth committing to.
        new WaveEntry { label = "scavengers", startTime = 35f, endTime = 155f, interval = 22f,
                        enemyType = "Scavenger", count = 2, shape = SpawnShape.Ring },

        // First Dropship: a grunt source you can switch off by killing it.
        new WaveEntry { label = "dropship", startTime = 45f,
                        enemyType = "Dropship", count = 1, shape = SpawnShape.Clump },

        // Armour arrives. The swipe stops being enough and the Blast earns its place.
        new WaveEntry { label = "armour", startTime = 60f,
                        enemyType = "Tank", count = 3, shape = SpawnShape.Ring },

        new WaveEntry { label = "armour", startTime = 65f, endTime = 155f, interval = 25f,
                        enemyType = "Tank", count = 2, shape = SpawnShape.Clump },

        new WaveEntry { label = "dropships", startTime = 80f,
                        enemyType = "Dropship", count = 1, shape = SpawnShape.Clump },

        // Mechs: the volley threat, and the first thing that punishes standing still.
        new WaveEntry { label = "mechs", startTime = 90f, endTime = 155f, interval = 35f,
                        enemyType = "Mech", count = 1, shape = SpawnShape.Clump },

        // Late push. Ahead means it lands in front of wherever you are running.
        new WaveEntry { label = "push", startTime = 120f,
                        enemyType = "Grunt", count = 12, shape = SpawnShape.Ahead,
                        commanderEvery = 1 },

        new WaveEntry { label = "push", startTime = 130f,
                        enemyType = "Dropship", count = 2, shape = SpawnShape.Clump },

        new WaveEntry { label = "push", startTime = 145f,
                        enemyType = "Mech", count = 2, shape = SpawnShape.Clump },

        // Bruisers arrive after the Mech has taught you to keep moving, and ask the
        // opposite: something that wants to be close, that you have to read rather
        // than outrun. Two of them, so the second lands while the first is winding up
        // — and Ahead puts one in your path rather than behind you, which is the only
        // placement a punch this slow can survive.
        new WaveEntry { label = "bruisers", startTime = 105f, endTime = 155f, interval = 40f,
                        enemyType = "Bruiser", count = 1, shape = SpawnShape.Ahead },

        new WaveEntry { label = "push", startTime = 150f,
                        enemyType = "Bruiser", count = 2, shape = SpawnShape.Ahead },

        // The finale.
        new WaveEntry { label = "BOSS", startTime = 160f,
                        enemyType = "Abomination", count = 1, shape = SpawnShape.Clump },
    };

    [Header("Survival")]
    [Tooltip("Grace after any hit. Without it a swarm deletes you in a single frame.")]
    public float hitInvulnerability = 0.5f;

    [Tooltip("Grace after shrinking a tier, so a death is a setback rather than a spiral.")]
    public float shrinkInvulnerability = 1.5f;

    [Tooltip("How far the shrink shockwave reaches, scaled by your size.")]
    public float shockwaveRadius = 5f;
    public float shockwaveForce = 16f;

    [Header("Swarm — upgrade")]
    [Tooltip("Seconds between discharges. Long on purpose: this is meant to go off " +
             "while you are busy with something else, so it has to be an event.")]
    public float swarmInterval = 5f;

    [Tooltip("Damage per particle, before size scaling. Set to match a size-1 swipe — " +
             "the upgrade's worth is that it fires unattended, not that a hit is big.")]
    public float swarmDamage = 13f;

    [Tooltip("How far it looks for targets, on the flat ground plane. Roughly the " +
             "screen at the zoom of the first few sizes.")]
    public float swarmRange = 11f;

    [Tooltip("Seconds the particles circle the kaiju before discharging. This is the " +
             "whole readability of the ability — without it they simply appear.")]
    public float swarmFormSeconds = 0.55f;

    public float swarmSpeed = 9f;

    [Tooltip("How hard a particle can turn. High enough to actually catch a Scavenger.")]
    public float swarmTurn = 55f;

    [Tooltip("Seconds after launch before a particle gives up.")]
    public float swarmLife = 3.5f;

    [Tooltip("How close counts as a hit, on the flat plane.")]
    public float swarmHitRadius = 0.5f;

    [Header("Toxin — upgrade")]
    [Tooltip("Damage per tick, before size scaling. Very small on purpose: this is " +
             "attrition on anything that follows you, not a weapon you point.")]
    public float toxinDamage = 1.6f;

    [Tooltip("Seconds between damage checks. Charged once per tick however many " +
             "overlapping puffs an enemy is standing in.")]
    public float toxinTickInterval = 0.5f;

    [Tooltip("Seconds a puff lingers after being dropped. This is the length of the wake.")]
    public float toxinCloudLife = 2f;

    [Tooltip("Cloud radius at size 1, in world units.")]
    public float toxinRadius = 2.2f;

    [Tooltip("Radius scales by (player scale ^ this). 0.5 is square root. At 1.0 the " +
             "area quadruples by size 5, which is the trap the camera zoom, the food " +
             "magnet and the stomp all fell into first.")]
    [Range(0f, 1f)] public float toxinRadiusExponent = 0.5f;

    [Tooltip("Seconds between dropped puffs. Shorter makes a smoother trail and more " +
             "objects; this is the framerate knob if the wake ever costs anything.")]
    public float toxinEmitInterval = 0.22f;

    [Header("Boss roar knockback")]
    [Tooltip("How far the roar throws the kaiju, in world units, if nothing is in the " +
             "way. Roughly half the screen at the zoom you are at by the time the boss " +
             "arrives. A building stops you early — which is the interesting outcome.")]
    public float knockbackDistance = 12f;

    [Tooltip("Seconds the throw lasts, decaying to a stop. Launch speed is derived " +
             "from this and the distance, so shortening it makes the same throw more " +
             "violent rather than shorter.")]
    public float knockbackSeconds = 0.55f;

    [Tooltip("Damage to a building the kaiju is thrown into. Runs through the normal " +
             "size-versus-class table, so this is the number before that multiplier: " +
             "at size 5 it flattens anything up to your own class and takes a real " +
             "bite out of the Core.")]
    public float knockbackImpactDamage = 120f;

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

    [Tooltip("Kaiju collision ellipse as a fraction of its own height, so it tracks " +
             "growth instead of being a fixed number the art outgrows.\n\n" +
             "Deliberately near the feet rather than the body: clipping a shoulder " +
             "through a tower reads as a big monster in a tight street, while being " +
             "stopped by a gap you can see through reads as a broken game. When in " +
             "doubt, smaller.")]
    public Vector2 playerFootprintFraction = new Vector2(0.30f, 0.15f);

    [Tooltip("Kaiju mass at size 1, against enemy masses of roughly 1 to 9. Scales with " +
             "the square of size, so infantry never shove you and the gap widens as you grow.")]
    public float playerMass = 25f;

    [Header("Swipe — the auto attack")]
    public float swipeDamage = 13f;
    public float swipeCooldown = 1.3f;

    [Tooltip("Reach in world units at size 1, measured from the kaiju's edge outward. " +
             "Deliberately short: melee should mean getting close, with reach coming " +
             "from upgrades. Scales with size so the animation reaches what it hits.")]
    public float swipeRange = 1.3f;

    [Tooltip("Width of the hit arc in degrees, centred on facing.")]
    [Range(30f, 360f)] public float swipeArc = 130f;

    [Tooltip("Gap between the hits of a multi-hit swipe. Short enough to read as one " +
             "flurry, long enough that each hit is visible.")]
    public float swipeBurstInterval = 0.13f;

    [Tooltip("Delay between the swing starting and its damage landing, so the hit lands " +
             "on the contact frame instead of before the arm has moved. The swipe clip " +
             "is 6 frames at 12 fps, so 0.20 is roughly frame 3. Retime this if the art " +
             "changes where contact happens.")]
    [Range(0f, 0.5f)] public float swipeContactDelay = 0.20f;

    [Tooltip("Draw the swipe arc briefly. A tuning aid, replaced by real VFX in Stage 9.")]
    public bool showSwipeArc = true;

    [Header("Blast — the manual special")]
    [Tooltip("One big number rather than chip damage: this is the answer to armour.\n\n" +
             "This is per beam, and Prism can put eight of them out at once, on top of " +
             "the tier damage multiplier and Beam's own stack. Read any change here as " +
             "a change to all eight.")]
    public float blastDamage = 30f;
    public float blastCooldown = 6f;
    public float blastRange = 14f;

    [Tooltip("Beam thickness as a fraction of the kaiju's height, so it grows with you. " +
             "Applies to the damage band as well as the drawing, so what you see is what " +
             "it hits.\n\n" +
             "Halved from 0.8 once Prism went to eight beams: at size 5 that was eight " +
             "bands almost five units wide, which is most of the screen and no aim.")]
    [Range(0.1f, 1.5f)] public float blastWidthFraction = 0.4f;

    [Tooltip("Height the beam is centred on, as a fraction of the kaiju's height. 0.5 " +
             "is mid-body. Firing from the head looked disconnected from a damage band " +
             "that sits on the ground, and the gap widened as the kaiju grew.")]
    [Range(0f, 1f)] public float blastOriginFraction = 0.5f;

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

        // Prism splits the Blast: 1 beam, then 2 opposed, then a cross, then an
        // eight-point star. It replaced a swipe reach-and-cone upgrade, which was
        // almost impossible to read in play — a wider cone looks like nothing, while
        // suddenly firing behind yourself is unmistakable.
        new UpgradeType { id = UpgradeId.Prism,   displayName = "Prism",
                          effect = "Blast splits: behind you, then a cross, then a star",
                          perStack = 1f, maxStacks = 3,
                          weight = 1f, colour = new Color(0.95f, 0.85f, 0.45f) },

        // 12% per stack was under the threshold where a single pickup registers —
        // it read as "maybe". 20% is felt on the pickup, and five of them roughly
        // two and a half times your speed, which is a whole build rather than a trim.
        new UpgradeType { id = UpgradeId.Speed,   displayName = "Speed",
                          effect = "Move 20% faster", perStack = 1.2f, maxStacks = 5,
                          weight = 1f, colour = new Color(0.5f, 0.9f, 0.75f) },

        new UpgradeType { id = UpgradeId.Stomp,   displayName = "Stomp",
                          effect = "Shockwave around you every few seconds", perStack = 1.4f,
                          maxStacks = 4, weight = 0.9f, colour = new Color(1f, 0.8f, 0.35f) },

        new UpgradeType { id = UpgradeId.Beam,    displayName = "Beam",
                          effect = "Blast hits 30% harder and further", perStack = 1.3f,
                          maxStacks = 4, weight = 0.9f, colour = new Color(0.5f, 0.85f, 1f) },

        // Swarm and Toxin are both unattended damage, and deliberately opposite in
        // shape: Swarm is a punctuation mark every five seconds that reaches across
        // the screen, Toxin is a constant that only touches what is already on you.
        //
        // Both grant an ability rather than modify one, so like Stomp the first stack
        // is worth its base numbers and only later stacks multiply.
        new UpgradeType { id = UpgradeId.Swarm,   displayName = "Swarm",
                          effect = "Homing particles discharge every few seconds",
                          perStack = 1.25f, maxStacks = 4,
                          baseProjectiles = 3, extraProjectilesPerStack = 1,
                          weight = 0.9f, colour = new Color(0.6f, 1f, 0.85f) },

        new UpgradeType { id = UpgradeId.Toxin,   displayName = "Toxin",
                          effect = "A poison cloud around you and in your wake",
                          perStack = 1.5f, maxStacks = 4,
                          weight = 0.9f, colour = new Color(0.55f, 0.9f, 0.35f) },

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
        // Art paths are wired ahead of the frames existing. A missing file loads as
        // null and the type stays on greybox, so the next delivery is a file drop
        // rather than another pass through here.
        new BuildingType { name = "Shack",    sizeClass = 0, tilesX = 1, tilesY = 1,
                           minHeightPx = 120, maxHeightPx = 180, hp = 30f,
                           foodDrops = 6,  foodScatter = 2.5f, weight = 30f,
                           artSprite = "Buildings/civilian_1x1",
                           colour = new Color(0.26f, 0.29f, 0.38f) },

        // Declared 1x2 rather than 2x1 to match the orientation the art is authored
        // in. The placer still flips half of them; a flipped one mirrors the sprite,
        // which lands exactly on the swapped footprint in this projection.
        new BuildingType { name = "Row",      sizeClass = 1, tilesX = 1, tilesY = 2,
                           minHeightPx = 150, maxHeightPx = 230, hp = 38f,
                           foodDrops = 9,  foodScatter = 3.2f, weight = 25f,
                           artSprite = "Buildings/civilian_1x2",
                           colour = new Color(0.22f, 0.31f, 0.39f) },

        new BuildingType { name = "Wide Low", sizeClass = 2, tilesX = 2, tilesY = 2,
                           minHeightPx = 190, maxHeightPx = 260, hp = 47f,
                           foodDrops = 14, foodScatter = 4.2f, weight = 20f,
                           artSprite = "Buildings/civilian_2x2",
                           colour = new Color(0.29f, 0.27f, 0.37f) },

        // Shares Wide Low's render, because the delivered set has one 2x2 civilian and
        // this roster has two. In greybox they read apart by height; in art they will
        // not. Either the artist adds a tall 2x2, or this moves to the 3x3 footprint
        // and takes civilian_3x3 — which is currently the one delivered civilian with
        // nothing pointing at it.
        new BuildingType { name = "Block",    sizeClass = 3, tilesX = 2, tilesY = 2,
                           minHeightPx = 380, maxHeightPx = 520, hp = 59f,
                           foodDrops = 22, foodScatter = 5.2f, weight = 15f,
                           artSprite = "Buildings/civilian_2x2",
                           colour = new Color(0.31f, 0.30f, 0.35f) },

        new BuildingType { name = "Tower",    sizeClass = 4, tilesX = 1, tilesY = 3,
                           minHeightPx = 620, maxHeightPx = 820, hp = 73f,
                           foodDrops = 34, foodScatter = 7f,  weight = 10f,
                           artSprite = "Buildings/civilian_1x3",
                           colour = new Color(0.25f, 0.26f, 0.42f) },

        // Laboratories are the only buildings that pay out power-ups, so they have to
        // read as prizes across a crowded street. Deliberately squat and a hue no
        // filler block uses — silhouette and colour are all greybox has to work with.
        new BuildingType { name = "Lab Small", sizeClass = 1, tilesX = 1, tilesY = 2,
                           minHeightPx = 200, maxHeightPx = 250, hp = 38f,
                           foodDrops = 8, foodScatter = 3f, upgradeDrops = 1, weight = 14f,
                           artSprite = "Buildings/laboratory_1x2",
                           colour = new Color(0.20f, 0.62f, 0.60f) },

        new BuildingType { name = "Lab Large", sizeClass = 3, tilesX = 2, tilesY = 2,
                           minHeightPx = 260, maxHeightPx = 330, hp = 59f,
                           foodDrops = 18, foodScatter = 4.5f, upgradeDrops = 2, weight = 10f,
                           artSprite = "Buildings/laboratory_2x2",
                           // Finer, faster, wider chips than the rest of the city —
                           // Samson's calibration against the delivered lab art, moved
                           // here from the Tuning asset so it is not a stray override.
                           debrisCount = 30, debrisSize = new Vector2(1f, 1.5f),
                           debrisForce = new Vector2(2.5f, 5f), debrisSpread = 62f,
                           colour = new Color(0.24f, 0.72f, 0.68f) },

        // Reactors. Twice the health of the ordinary building at their footprint, and
        // on death a pulse that only hurts enemies. Placed deliberately and spaced so
        // two are never on screen together — weight is unused for these.
        //
        // Small pulse at 20 clears Grunts (12 hp) outright and cannot finish anything
        // else: a Tank takes 14 through 6 armour, a Commander 20 of 120.
        new BuildingType { name = "Reactor Small", sizeClass = 0, tilesX = 1, tilesY = 1,
                           minHeightPx = 170, maxHeightPx = 220, hp = 60f,
                           foodDrops = 8, foodScatter = 3f, weight = 0f, isReactor = true,
                           pulseDamage = 20f, pulseRadius = 24f,
                           artSprite = "Buildings/reactor_1x1",
                           colour = new Color(0.92f, 0.62f, 0.20f) },

        // Large pulse at 130 kills everything up to Tank class — Tank, Dropship and
        // Commander all fall — while a Mech survives on 140 hp behind 12 armour.
        new BuildingType { name = "Reactor Large", sizeClass = 3, tilesX = 2, tilesY = 2,
                           minHeightPx = 300, maxHeightPx = 400, hp = 118f,
                           foodDrops = 20, foodScatter = 5f, weight = 0f, isReactor = true,
                           pulseDamage = 130f, pulseRadius = 32f,
                           artSprite = "Buildings/reactor_2x2",
                           colour = new Color(0.96f, 0.45f, 0.18f) },

        // The Core. One per run, and the only building that is genuinely a size-5 job.
        // Class 4 and 700 hp puts it a full class above a size-4 kaiju, where the delta
        // table already drops damage to a third — about 11 seconds at size 5, three
        // quarters of a minute at size 4, and an outright wall below that.
        //
        // Pulse at 300 clears a Mech outright through its 12 armour, and takes roughly
        // a quarter off the Abomination — so felling it before the boss lands is a real
        // strategic play rather than just more damage.
        new BuildingType { name = "Reactor Core", sizeClass = 4, tilesX = 3, tilesY = 3,
                           minHeightPx = 520, maxHeightPx = 660, hp = 700f,
                           foodDrops = 40, foodScatter = 9f,
                           weight = 0f, isReactor = true, unique = true,
                           pulseDamage = 300f, pulseRadius = 40f,
                           artSprite = "Buildings/reactor_3x3",
                           colour = new Color(1f, 0.30f, 0.22f) },
    };

    [Tooltip("How many reactors to scatter through the arena.")]
    public int reactorCount = 4;

    [Tooltip("Minimum distance between reactors in world units. 26 is wider than the " +
             "screen diagonal at maximum zoom-out, so two can never be visible at once.")]
    public float reactorMinSpacing = 26f;

    [Tooltip("Damage multiplier by (your size - the building's class), from -4 to +4. " +
             "The middle entry is your own class and is always 1. Left of it is the wall: " +
             "each step up in class roughly triples the work. Right of it is the payoff.")]
    public float[] damageVsBuildingByDelta = { 0.025f, 0.05f, 0.125f, 0.33f, 1f, 2f, 3f, 4f, 5f };

    [Tooltip("Progress bar over enemies you have damaged. Vanishes once you outgrow them.")]
    public bool showEnemyHealthBars = true;

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
