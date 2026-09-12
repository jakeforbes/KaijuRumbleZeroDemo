# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

Kaiju Rumble Zero — a Unity **6000.6.0f1** URP 2D vertical-slice prototype. One three-minute
run: grow the kaiju by eating, smash the city, survive waves, kill the boss. All gameplay
code is plain C# MonoBehaviours under `Assets/KRZ/Scripts/` with no assembly definitions and
no namespaces.

`README.md` holds the player-facing detail — full control scheme, the debug-key table, and
the art/FX inventory. Read it rather than re-deriving those.

## Running and verifying

There is no test suite, no build script and no CLI entry point. Verification is Unity Play mode.

- Open the project in Unity 6000.6.0f1, open `Assets/Scenes/SampleScene.unity`, press Play.
- **The scene is empty and that is correct.** `GameBootstrap` builds everything from code.
- `Gym` scene lays every building type out in a labelled row with no waves — use it for art,
  collider and damage-state review.
- C# compiles when Unity regains focus. `.csproj`/`.slnx` are generated and gitignored; do not
  hand-edit or commit them.

In-editor checks, run from the Unity menu during a Play session:

| Menu | Purpose |
| --- | --- |
| `KRZ/Audio/Check Playback` (`Ctrl+Alt+T`) | Fires a real game event through `AudioEvents` and samples output |
| `KRZ/Audio/Check Boss Encounter` (`Ctrl+Alt+B`) | Spawns the Abomination and exercises the encounter + music routes |
| `KRZ/Select Tuning Asset` | Selects the Tuning asset for live tweaking |

When a change cannot be verified in Play mode, say so explicitly rather than implying it ran.

## The one hard rule: Tuning

`Assets/KRZ/Scripts/Tuning.cs` (~1,400 lines of `[Header]`-grouped fields) is the **source of
truth for every gameplay number**. Change values there.

`Assets/KRZ/Resources/Tuning.asset` must keep holding **only the four sound-bank references**.
Unity serializes the *whole* asset the moment anything dirties it, and a serialized value
silently wins over the C# field initialiser — after which edits to `Tuning.cs` do nothing, with
no error and nothing in the console. This has twice cost real work (a pinned `moveSpeed`, and a
save that would have reverted two enemy types, two upgrades and the boss).

Live tweaking in the Inspector while playing is fine and encouraged — just treat it as a
scratchpad, then put the number in `Tuning.cs` and never let the asset save to disk.

## Architecture

**Everything is built from code at runtime.** `GameBootstrap` (`[RuntimeInitializeOnLoadMethod]`,
`AfterSceneLoad`) clears whatever the scene shipped with, then constructs camera, ground, city or
gym, player, wave director and HUD. Nothing is wired in the Editor, there are no prefabs, so
nothing drifts out of sync and there is no scene YAML to merge-conflict over. `GameBootstrap.Restart()`
reloads the scene and re-triggers `Launch()` manually, since it only fires once per play session.

**Data-driven definitions.** Enemies, buildings, upgrades and wave beats are serializable types
(`EnemyType`, `BuildingType`, `UpgradeType`, `WaveEntry`) held as arrays on `Tuning`. Adding a
species or a pacing beat is a data entry, not a new class. `WaveDirector` walks the `waves` list
against its own `Clock`, which cheats can scrub.

**Static registries and singletons.** `Enemy.All`, `Building.All`, `Food.All` are static lists;
`GameBootstrap`, `WaveDirector`, `PlayerProgress`, `PlayerUpgrades`, `OccluderFade`,
`RuntimeSoundPlayer`, `UriesArt` expose `Instance`. Pooled/parented systems cache a static `root`
transform. **Any new static state needs a `Reset()` called from `GameBootstrap.Awake()`** — the
existing block there is the list to extend, or a restart inherits the previous run's state.

**Sorting and collision.** The world is screen space: X horizontal, Y depth up the screen,
vertical movement scaled by `isoSquash` for the 2:1 projection. Sprites sort by pivot Y via the
camera's transparency sort axis, and every pivot sits at the object's ground contact point — that
is what makes walking behind a building work. Collision footprints are always the **ground area**
an object occupies, never sprite bounds, so a kaiju overlaps a tower's upper floors without
colliding with them.

`Physics2D.queriesHitTriggers` is set **false** globally in `ClearScene()`. Every plain
`OverlapCircle` in the project is a placement-clearance check, and missiles carry a trigger
collider only so player attacks can find them — leaving it on made a passing volley silently
reject spawns, which is how the boss once went missing. Player sweeps pass an explicit
`ContactFilter2D` with `useTriggers` set, so they are unaffected. Don't flip this back.

**Art loading.** `DirectionalArt` loads PNGs from `Resources` as `Texture2D` and builds `Sprite`s
in code, deliberately *not* loading Sprite assets — pivot and pixels-per-unit come from the art
spec rather than from `.meta` import settings, so a fresh clone works with no setup step. Three
art packages arrived with three different direction-naming conventions (`DirectionStyle`:
`LongLower`/`ShortLower`/`ShortUpper`), so a new delivery is an `ArtConfig` path template plus a
clip list, not another branch. When art is missing, `GreyboxArt` procedural fallbacks stand in —
code work is possible without the art packages.

**FX are procedural.** Shaders live in `Assets/KRZ/Resources/*.shader`; gameplay builds the meshes
and visual objects at runtime. Two URP renderer features, `CityAtmosphereFeature` and
`StompDistortionFeature`, are installed on **both** `Assets/Settings/PC_Renderer.asset` and
`Mobile_Renderer.asset` — changes to either must be made in both. Their settings (night 0.92,
bloom 0.65, fog 0.58) are renderer state, separate from gameplay `Tuning.cs`.

## Repo conventions

- Unity YAML (`*.unity`, `*.prefab`, `*.asset`, `*.meta`, …) is marked `-merge` in
  `.gitattributes` — never auto-merged. Resolve by hand or regenerate.
- The ~213MB of art packages under `Assets/KRZ/Resources/` are committed **on purpose**.
- `/KaijuRumbleZero/` at the repo root is a stray nested repo, gitignored, not part of the project.
- Existing code carries dense explanatory comments on the *why* of non-obvious decisions
  (see `ClearScene`, `DirectionalArt`, `WaveDirector.BeginRecovery`). Match that density —
  comments here record traps that already bit, not restatements of the code.
