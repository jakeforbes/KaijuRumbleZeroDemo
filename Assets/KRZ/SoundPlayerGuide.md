# Sound Player

## Background music

Open **KRZ > Audio > Background Music**. Assign Default Game, Boss Approaching, and Boss Fight clips, their individual volumes, master music volume, crossfade duration, and optional mixer output. Boss Approaching and Boss Fight are linked to the matching files in Assets/Music; Default Game still needs a clip. All music loops.

Play creates a separate **Background Music Player** with two AudioSources for crossfades. Normal play uses Default Game. Bosses spawn on clear ground near the generated playfield perimeter, choosing a far edge away from the player rather than the old nearby spawn ring. Boss spawning starts Boss Approaching music while the camera pans to the boss, follows its brief approach, then returns to the player. Gameplay continues during the reveal, and the player is invulnerable while the camera is away. Music continues using real time. Tuning exposes Boss Intro Pan Seconds, Boss Intro Hold Seconds, and Boss Intro Return Seconds (defaults: 1.25, 2, 1.25 seconds). Game speed stays unchanged, and the player camera is restored, including on cancellation or restart. Multiple simultaneous introductions are queued.

After the reveal, Boss Fight starts only when both the player and boss gameplay positions enter the gameplay camera, using Boss Fight Screen Margin in Music Settings. Large sprites, shadows, and transparent padding no longer prevent the trigger. Attacking from off-screen does not trigger it, and the cinematic camera cannot trigger it early. Once active, the fight stays active if either character leaves the frame; defeating/removing the boss returns to the appropriate music for any remaining bosses or to Default Game. Abomination is recognized automatically; enable Is Boss on other enemy types.

The player exposes **PlayDefaultGame**, **PlayBossApproaching**, **PlayBossFight**, and **ResumeAutomaticMusic** methods for UnityEvents/scripted encounters, plus an **On Music State Changed** UnityEvent. Manual events hold their selected state until another event, Resume Automatic Music, or a real boss introduction. They are also available in the runtime component's context menu for testing. A future pre-spawn warning can call PlayBossApproaching, then ResumeAutomaticMusic when the boss spawns. Currently the automatic approach phase starts when a boss actually exists in the scene.

Unassigned boss tracks fall back to Default Game. An unassigned Default Game means silence during normal play. Editing the Music Settings asset updates playback during Play and saves the settings for future runs.

## Start here: edit sounds before Play

Choose **KRZ > Audio > Player Sounds** (or Enemy Sounds, Building Sounds, Food Sounds) from Unity's top menu. These ready-made templates live in **Assets/KRZ/Audio** and are already assigned on the Tuning asset. Select one, expand an action, and add clips from Assets/Sounds. There is no need to place anything in the scene. Press Play after configuring the template; the generated objects receive a copy automatically. All clip slots start empty for you to choose sounds.

For different enemy species, duplicate Enemy Sounds in the Project window, customize the copy, and assign it to that enemy type's Sounds field in Tuning.

The player now has **Swipe Hit Enemy** and **Swipe Hit Building** actions. Assign different clips to these in Player Sounds. Each has independent Size 1–5 overrides. Existing Swipe Hit clips and size settings were copied into both entries so your tuning is preserved. A swipe plays each relevant impact once: hitting several enemies plays one enemy impact; hitting both an enemy and a building plays both. Missing every target plays neither. Target-owned hurt/building damage sounds remain separate from these player impact sounds.

## Runtime playback

### Different sounds for the five player sizes

In **KRZ > Audio > Player Sounds**, expand an action and enable **Use Size Settings**. Expand **Size 1**, **Size 2**, **Size 3**, **Size 4**, or **Size 5**, then enable **Override Settings** for the levels you want to customize. Each level supports its own clips, volume, fixed/random pitch and range, sound size, echo, reverb, enabled state, and cooldown.

New size entries copy the action's current defaults, including assigned clips. Once overridden, each level is independent. Levels with Override Settings off use Default settings; turning Use Size Settings off restores defaults for every level. The current player size is checked each time a sound starts, so growing and shrinking automatically switch settings. Grow/shrink sounds use the new size. Sounds already playing finish with their original settings. Objects without PlayerProgress continue using defaults. Master volume and other object-wide controls still apply to all levels.

Edit the saved Player Sounds template before Play, then restart Play to copy your changes into the generated player.

Pressing Play creates a **Sound Player** object in the Hierarchy. Its **Runtime Sound Player** component owns eight pre-created child AudioSources, growing the pool up to Voice Limit when needed. The generated camera owns the AudioListener. Gameplay objects keep their Sound Player settings and send sounds to this shared playback service. You do not need to add AudioSources by hand.

Select the runtime Sound Player to see Active Voices, Sounds Played, Last Clip, and Last Action. Expand its children to inspect actual AudioSources and effects. Completed voices are reused; each playback resets its effects. Sounds can finish after an enemy or pickup disappears. The pool is destroyed with the scene and recreated on restart.

**KRZ > Audio > Check Playback (Play mode)** triggers the player's configured Footstep through the gameplay event route and measures output at the AudioListener. It reports PASS/FAIL in the Console and writes a diagnostic result under Temp/SoundPlayerValidation. A PASS verifies Unity produces audio; device volume and the selected speakers remain outside this check. The footstep action needs an assigned, enabled clip and must be off cooldown.

## Adding the component to your own objects

**Food Pickup** in Player Sounds plays when a dropped bit reaches the player, whether it came from an enemy or a building. It supports all five size overrides and continues at maximum size. Assign clips to Food Pickup; its cooldown can limit overlapping pickup sounds. When the player has no Food Pickup clip assigned, the Food Sounds template remains the fallback. Both sounds do not play for the same pickup.

1. Add **Sound Player** to the same GameObject as Enemy, PlayerController, PlayerAttack, PlayerProgress, Building, or Food. Its Inspector lists the sound actions declared by those components, combining them when several are attached.
2. Expand an action. Drag clips from **Assets/Sounds** into its Clips list, or use **Add clips from Sounds...**. Multiple clips are selected randomly; empty slots are ignored.
3. Set per-action volume, fixed pitch or Randomize Pitch with a min/max range, echo, reverb preset, cooldown, and Sound Size. Master Volume affects every action on the object. Output accepts an optional Audio Mixer group for additional effects. Spatial Blend defaults to 2D for this game.
4. Use **Test sound (Play mode)** to audition the action with its effects. Test playback obeys the action's enabled switch and cooldown.

Sound Size runs from -1 (smaller/tinnier) through 0 (original) to +1 (larger/bassier). It combines pitch shifting with bass removal at the small end and treble removal at the large end. This changes perceived weight; it does not synthesize bass absent from the original clip. Random pitch range is applied before this size adjustment.

## This demo's generated objects

The demo creates gameplay objects in code when Play starts. To keep your setup:

- Configure a Sound Player, then choose **Save sound template...** before leaving Play mode. This saves only the audio setup as a prefab.
- On **Assets/KRZ/Resources/Tuning.asset**, assign that prefab to Player Sounds, Enemy Sounds, Building Sounds, or Food Sounds.
- An enemy type's **Sounds** field overrides the common Enemy Sounds template.
- Stop and restart Play to apply template assignments to freshly generated objects. Editing a spawned component affects that instance; saving its template preserves those changes.
- You can also create an empty GameObject outside Play, add Sound Player, and use manual actions to prepare a template without running the game.

Enemy actions: spawn, footsteps while moving, attack, hit, blocked hit, death, squish, and pushed. Player actions: footsteps, swipe, swipe hit, hurt, grow, shrink, and lose. Buildings: hit and destroyed. Food: pickup. A squish plays Squish instead of Enemy Death. Death and pickup voices finish after their objects disappear. Independent overlapping voices retain their own pitch and effects. Max Voices limits each object's simultaneous sounds; a new sound replaces the oldest at the limit. Scene reload clears these voices.

Detected rows are preserved if components are removed, so clip assignments are not lost. You can remove unused/manual rows. Leaving an action unassigned permits the existing global AudioEvents fallback; disabling an assigned action silences that object's action.

## Adding new gameplay scripts

Unity cannot automatically infer meaningful sound timing from arbitrary methods. Declare the actions a component emits, then emit at the actual gameplay moment:

```csharp
[SoundActions(Sfx.EnemyAttack)]
public class CustomAttacker : MonoBehaviour
{
    void Attack()
    {
        AudioEvents.Play(Sfx.EnemyAttack, transform.position, owner: gameObject);
    }
}
```

Add new action identifiers at the end of the Sfx enum to preserve existing serialized values. For UnityEvents or animation events, call SoundPlayer.PlayAction with the exact enum name, such as EnemyAttack, and add the corresponding manual action row. Keep one row per action.

The **KRZ > Audio > Check Boss Encounter (Play mode)** regression check requires a fresh Play session. It spawns a temporary stationary test boss at the edge, verifies unchanged gameplay speed and player invulnerability during the introduction, then moves that test boss into the camera to verify the fight-state transition and audio output. It removes its test boss afterward. Results are written to Temp/SoundPlayerValidation/boss-encounter-result.txt.
