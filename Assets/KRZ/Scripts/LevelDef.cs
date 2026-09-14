using UnityEngine;

/// <summary>
/// One level: how big the city is, what arrives, and what ends it.
///
/// A static table in code rather than an array on Tuning, for the same reason
/// CharacterType.Roster is — see the note there. These are layout and pacing wiring
/// rather than numbers anyone tunes with the game running, and an array on the asset
/// would be one stale Library copy away from silently ignoring every edit.
///
/// The full three-minute run is still here as the second entry, unchanged. Nothing about
/// this describes progression beyond a single hop: the Proving Ground returns to itself,
/// which is enough to test that clearing a level, choosing a reward and carrying it into
/// the next one works at all.
/// </summary>
public class LevelDef
{
    public string name = "level";

    /// <summary>City blocks. 0 on either axis falls back to Tuning's own figures.</summary>
    public int blocksX, blocksY;

    /// <summary>
    /// When the level's closing enemy walks on. 0 means the level has none and its end
    /// condition lives in the wave list, which is how the full run's Abomination arrives.
    /// </summary>
    public float bossSeconds;

    /// <summary>Matched against Tuning.enemyTypes. Killing one of these opens the portal.</summary>
    public string bossType = "";

    /// <summary>
    /// Upgrades held back from the drop table. The Proving Ground withholds Swarm and
    /// Stomp so that the portal's choice between them is the first time either appears —
    /// a reward you were already handed is not a choice.
    /// </summary>
    public UpgradeId[] blacklist = System.Array.Empty<UpgradeId>();

    /// <summary>The level's own pacing. Null uses Tuning.waves, which is the full run's.</summary>
    public WaveEntry[] waves;

    public bool Blacklists(UpgradeId id)
    {
        foreach (var b in blacklist) if (b == id) return true;
        return false;
    }

    /// <summary>
    /// Which level a run builds. Outside any Reset() on purpose: the portal sets it before
    /// the scene reloads, exactly as the character select does with its own index.
    /// </summary>
    public static int SelectedIndex;

    public static LevelDef Current => All[Mathf.Clamp(SelectedIndex, 0, All.Length - 1)];

    /// <summary>
    /// Enemies that belong to a level rather than to the main roster, declared here for the
    /// same reason the levels themselves are: an entry added to Tuning.enemyTypes can be
    /// silently absent at runtime when Unity serves a stale serialized copy of the asset.
    /// That is exactly what happened to this one — the wave fired on schedule and logged
    /// "wants enemy type 'Mech Lieutenant', which does not exist". GameBootstrap.FindType
    /// checks here after the Tuning roster.
    /// </summary>
    public static readonly EnemyType[] ExtraEnemyTypes =
    {
        // The Proving Ground's closing enemy: a Mech at half strength, on the same art and
        // the same missile volley, tinted pale so it reads as a lesser version of the thing
        // it is. Deliberately not flagged isBoss — that is the Abomination's win condition,
        // and this one ends a level rather than the run.
        //
        // Tuned to be beatable at tier 1: thirty seconds of Grunts and Troopers is not much
        // food, so it needs enough hp to require the whole kit and not enough to outlast it.
        // Currently soft on purpose. These numbers are set for testing the level flow —
        // clear, portal, choose, next level — not for balance, so the Lieutenant is a
        // checkpoint you walk through rather than a fight. Everything here wants raising
        // once the loop itself is proven: hp, the volley, and both damage figures.
        new EnemyType { name = "Mech Lieutenant", sizeClass = 2, hp = 35f, armour = 5f,
                        contactDamage = 8f, moveSpeed = 1.6f, attackRange = 1.6f,
                        attackCooldown = 1.9f, foodDrops = 6, foodScatter = 3f,
                        special = SpecialAction.MissileVolley,
                        missileDamage = 6f, volleyCount = 4,
                        bodyPx = 384, colour = new Color(0.55f, 0.45f, 0.72f),
                        artFolder = "Mech", artPrefix = "mech", artDisplayPx = 300,
                        footprintFraction = 0.34f, artFrameDigits = 4, artFirstFrame = 1,
                        artTint = new Color(0.72f, 0.68f, 1f) },
    };

    public static readonly LevelDef[] All =
    {
        // A sixteenth of the full run's area — a quarter on each axis — and thirty seconds
        // long. Small enough to cross in a few seconds at size 1, which is the point: the
        // whole loop, clear to portal to choice to next level, is testable in under a minute.
        new LevelDef
        {
            name = "Proving Ground",
            blocksX = 4,
            blocksY = 4,
            bossSeconds = 30f,
            bossType = "Mech Lieutenant",
            blacklist = new[] { UpgradeId.Swarm, UpgradeId.Stomp },
            waves = new[]
            {
                // Compressed to fit thirty seconds. Everything stops at 28 so the streets
                // are thinning as the Lieutenant walks on rather than still filling: on a
                // map this small a Tank squad arriving alongside it would be the whole level
                // in one screen.
                new WaveEntry { label = "grunts", startTime = 2f, endTime = 28f,
                                interval = 4f, enemyType = "Grunt", count = 3,
                                shape = SpawnShape.Clump },

                new WaveEntry { label = "troopers", startTime = 12f, endTime = 28f,
                                interval = 6f, enemyType = "Trooper", count = 3,
                                shape = SpawnShape.Ahead },
            },
        },

        // The original run, untouched. Null waves means Tuning.waves, and no bossSeconds
        // because its Abomination is a beat in that list like everything else.
        new LevelDef { name = "Full Run", bossType = "Abomination" },
    };
}
