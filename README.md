# Kaiju Rumble Zero — vertical slice prototype

Unity **6000.6.0f1**, URP, 2D sprites. Two-day throwaway prototype.

## Running it

1. Open the project in Unity 6000.6.0f1.
2. Open `Assets/Scenes/SampleScene.unity`.
3. Press **Play**.

**The scene is empty and that is correct.** There is no authored scene content and no
prefabs. `GameBootstrap` builds the camera, ground, city, player and HUD from code
when you press Play, in whatever scene is open. Nothing is wired in the Editor, so
nothing drifts out of sync and there is no scene file to merge-conflict over.

Greybox art is generated at runtime too — no art assets are needed to run it.

## Tuning

Every gameplay number lives on one asset: `Assets/KRZ/Resources/Tuning.asset`
(or menu **KRZ → Select Tuning Asset**). Select it and edit in the Inspector,
including while the game is running — changes take effect immediately and persist.

The bootstrap never writes to it, so rebuilding the arena never costs you a tweak.

## Controls

| Input | Action |
| --- | --- |
| WASD / left stick | Move (8-way on keys, analog on pad) |
| — | Swipe fires automatically on a cooldown |

## Debug keys

| Key | Action |
| --- | --- |
| `F1` | Toggle debug HUD |
| `F2` / `F3` | Grow / shrink one size |
| `F4` | Spawn a swarm of Grunts (`Shift+F4` for Tanks) |
| `F5` | Kill all enemies |
| `F6` | Toggle god mode |
| `F10` | Restart the run |
| `F12` | Draw collision footprints |
| `[` `]` | Slow down / speed up time |
| `\` | Reset time scale |

Debug output and cheats are switched off with `showDebugHud` and `enableCheatKeys`
on the Tuning asset.

## Layout

```
Assets/KRZ/
  Scripts/      all gameplay
  Editor/       creates the Tuning asset on first compile
  Resources/    Tuning.asset
Docs/           art spec and build plan (open the .html files in a browser)
```

## Sprite sorting

The world is screen space: X is horizontal, Y is depth up the screen, and vertical
movement is scaled by `isoSquash` to match the 2:1 projection. Sprites sort by their
pivot's Y through the camera's transparency sort axis, and every pivot sits at the
object's ground contact point. That is what makes walking behind a building work.

Collision footprints are always the ground area an object occupies, never the sprite
bounds — a kaiju overlaps a tower's upper floors without colliding with them.
