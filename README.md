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

**`Assets/KRZ/Scripts/Tuning.cs` is the source of truth for every gameplay number.**
Change values there.

`Assets/KRZ/Resources/Tuning.asset` deliberately holds **only the four sound-bank
references**. It must stay that way.

### Please do not save values into Tuning.asset

Unity writes the whole asset out the moment anything dirties it — including just
having it selected in the Inspector while something else changes. A saved asset
serializes *every* field, and a serialized value silently wins over the C# field
initialiser. From then on, edits to `Tuning.cs` do nothing.

This has already cost the project real work twice:

- The asset pinned `moveSpeed: 7` for most of a day. Three separate rounds of
  slowing the player down had no effect in play, because the asset kept overriding
  them. It read as "the tuning isn't working."
- A later save serialized the full enemy, building and upgrade arrays. Merging it
  would have removed two enemy types and two upgrades and reverted the boss, with
  no error and nothing in the console — it would simply have looked like the
  features were broken.

Nothing warns you when this happens. If you need a value changed and don't want to
edit C#, ask — it is a one-line change.

**Live tuning while playing** still works: select the asset, edit in the Inspector,
watch the change immediately. Just don't let the edit get saved to disk — treat it
as a scratchpad for finding a number, then put the number in `Tuning.cs`.

## Controls

| Input | Action |
| --- | --- |
| WASD or arrows | Move, 8-way |
| Left stick | Move, analog |
| D-pad | Move, 8-way — identical to WASD |
| Space or E | Blast |
| A button or right trigger | Blast |
| — | The swipe fires automatically on a cooldown |

Keyboard and pad are live at the same time and never need switching between:
whichever of keys, stick or d-pad is pushed hardest is the one that steers. They
are compared rather than summed, so a controller sitting on the desk with a
drifting stick cannot bend a key press off its axis or quietly walk the kaiju
across the map on its own.

## Debug keys

| Key | Action |
| --- | --- |
| `F1` | Toggle debug HUD |
| `F2` / `F3` | Grow / shrink one size |
| `F4` | Spawn a swarm of Grunts (`Shift+F4` for Tanks) |
| `F5` | Kill all enemies |
| `F6` | Toggle god mode |
| `F7` | Grant a random power-up |
| `F8` | Spawn a Dropship (`Shift+F8` for a Scavenger) |
| `F9` | Spawn the Abomination |
| `F10` | Restart the run |
| `F11` | Spawn whoever currently carries power-ups — Commander below size 3, Elite Tank at or above it (`Shift+F11` Mech, `Ctrl+F11` Bruiser) |
| `F12` | Draw collision footprints |
| `,` `.` | Scrub the wave timeline back 15s / forward 30s |
| `[` `]` | Slow down / speed up time |
| `\` | Reset time scale |

Debug output and cheats are switched off with `showDebugHud` and `enableCheatKeys`
in `Tuning.cs`.

## Scenes

`SampleScene` is the game. `Gym` lays every building type out in a labelled row with
no waves running, for checking art, colliders and damage states in isolation.

## Layout

```
Assets/KRZ/
  Scripts/      all gameplay
  Editor/       creates the Tuning asset on first compile
  Resources/    Tuning.asset (sound refs only), art packages
Docs/           art spec and build plan (open the .html files in a browser)
```

## Sprite sorting

The world is screen space: X is horizontal, Y is depth up the screen, and vertical
movement is scaled by `isoSquash` to match the 2:1 projection. Sprites sort by their
pivot's Y through the camera's transparency sort axis, and every pivot sits at the
object's ground contact point. That is what makes walking behind a building work.

Collision footprints are always the ground area an object occupies, never the sprite
bounds — a kaiju overlaps a tower's upper floors without colliding with them.
