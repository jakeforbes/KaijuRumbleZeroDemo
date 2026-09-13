using UnityEngine;

/// <summary>
/// What the special button does for a character. The button, the cooldown and the HUD
/// readiness meter are shared; only what comes out differs.
/// </summary>
public enum CharacterSpecial
{
    /// <summary>Damage in a line, answered to armour. The Alien's and the default.</summary>
    Blast,

    /// <summary>Thrown out straight, chased home. See BoneBoomerang.</summary>
    BoneBoomerang,
}

/// <summary>
/// One playable kaiju: where its frames live and how it is drawn. Held as an array on
/// Tuning, the same way EnemyType and BuildingType are, so adding a character is a data
/// entry rather than a new class.
///
/// The art layout is the one CharacterSpriteBaker writes and the Uries package already
/// used: {folder}/{clip}/{direction}/{prefix}_{clip}_{direction}_{frame}. The only thing
/// that varies between characters is whether that folder carries a growth level.
/// </summary>
[System.Serializable]
public class CharacterType
{
    [Tooltip("Shown under the portrait on the character select grid.")]
    public string displayName = "Alien";

    [Tooltip("Resources folder holding the frames.\n\n" +
             "{level} is replaced with the growth tier, 1-5. Include it and the character " +
             "swaps to different art as it grows, which is what the Uries package ships. " +
             "Leave it out and one set of frames serves every tier, scaled up by " +
             "PlayerController.SetScale — which is what a baked rig does.")]
    public string artFolder = "Uries/Level_{level}";

    [Tooltip("Filename stem. {level} is substituted exactly as in artFolder.")]
    public string artPrefix = "uries_l{level}";

    [Tooltip("Frames per clip, in Clip order: idle, walk, swipe, blast, hit. " +
             "CharacterSpriteBaker writes these counts, so a baked character keeps the " +
             "default. Only a hand-delivered package should need to change it.")]
    public int[] frameCounts = { 4, 8, 6, 6, 3 };

    [Tooltip("On-screen size relative to the other characters.\n\n" +
             "The baker already frames every character to fill the same share of its canvas, " +
             "so this is for taste rather than for correction: nudge it when a kaiju is the " +
             "right height and still reads as too big or too small next to the rest. Changing " +
             "it costs nothing — no re-bake, it scales the sprite.")]
    [Range(0.25f, 5f)] public float displayScale = 1f;

    [Tooltip("Multiplied over the frames.\n\n" +
             "The Uries model is drawn almost entirely in white and pale grey, so a tint " +
             "lands on it cleanly. A baked rig carries its own albedo and wants white here " +
             "— tinting one of those repaints it rather than lighting it.")]
    public Color tint = Color.white;

    [Tooltip("Unlocked characters are playable and show their portrait. Everything else on " +
             "the select grid is drawn as a locked slot.")]
    public bool unlocked = true;

    [Tooltip("How far the select grid crops into the portrait frame.\n\n" +
             "Portraits are frame 0 of the south-facing idle, and how much of that 512px " +
             "canvas the character actually fills varies: a baked rig is framed to fill it, " +
             "while the Uries level 1 sits small because it grows into the frame over its " +
             "five levels. 1 shows the whole frame; raise it until the portrait reads.")]
    [Range(1f, 4f)] public float portraitZoom = 1f;

    /// <summary>
    /// Level 2's idle shipped broken in all five directions: frames 01 and 02 each contain
    /// two characters side by side — 420 px of occupied canvas against the 185 px the
    /// character actually fills — and frames 00 and 03 are crushed to half width. The loop
    /// was strobing between three silhouettes four times a second.
    ///
    /// With this set, level 2's idle borrows the walk cycle's two passing poses instead.
    /// Clear it the moment the artist redelivers that clip. It is a property of the Uries
    /// delivery specifically, which is why it lives on the character rather than in
    /// CharacterArt: no other character has the defect, and a baked one cannot.
    /// </summary>
    [Tooltip("Uries only: rebuild level 2's broken idle from the walk cycle. See the source " +
             "comment before changing.")]
    public bool patchLevel2Idle;

    [Tooltip("What the special button fires. Cooldown, input and the HUD meter are shared " +
             "across every character; only the effect changes.")]
    public CharacterSpecial special = CharacterSpecial.Blast;

    /// <summary>
    /// The playable roster, in character-select grid order.
    ///
    /// A static table rather than an array on Tuning, and deliberately so. Unity hands back a
    /// ScriptableObject's *serialized* state, and once a field has been written into the
    /// imported asset that value wins over the C# initialiser permanently — silently, with
    /// nothing in the console, and whether or not the .asset file on disk mentions it, because
    /// the stale copy lives in the Library cache. This array was caught by exactly that: the
    /// asset reported displayScale 1 while this file said 3, through a recompile, a reimport
    /// and four bakes.
    ///
    /// That is the same hazard CLAUDE.md records against Tuning.asset. Enemies and buildings
    /// live with it because their numbers genuinely want live tweaking; these are asset paths
    /// and canvas scales that nobody tunes with the game running, so nothing is lost by keeping
    /// them out of the Inspector and everything is gained by edits here actually taking effect.
    /// </summary>
    public static readonly CharacterType[] Roster =
    {
        new CharacterType
        {
            displayName = "Alien",
            artFolder = "Uries/Level_{level}",
            artPrefix = "uries_l{level}",
            // The same orange as Tuning.playerTint. It lives here rather than being read from
            // that field because it is a property of this character's white-and-grey model,
            // and applying it to a textured rig would repaint the rig.
            tint = new Color(1f, 0.62f, 0.26f),
            patchLevel2Idle = true,
        },
        new CharacterType
        {
            displayName = "Lizard",
            artFolder = "Lizard",
            artPrefix = "lizard",
            tint = Color.white,

            // Its blast is a lunge that throws the rig most of a canvas width sideways, and the
            // bake sizes every frame to hold the widest pose in the set — so the lizard bakes at
            // about a third of the height the other two do. Tripling here matches it to the
            // Alien without another bake. The sharper fix is a blast animation that stays put,
            // after which this goes back to 1.
            displayScale = 3f,
        },
        new CharacterType
        {
            displayName = "Skeleton",
            artFolder = "Skeleton",
            artPrefix = "skeleton",
            tint = Color.white,

            // Its blast clip is the rig's "cast" animation, which is a throw in all but name.
            special = CharacterSpecial.BoneBoomerang,
        },
    };
}
