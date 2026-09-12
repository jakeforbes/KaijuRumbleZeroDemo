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

The project includes sprite artwork; procedural fallback art and animated effects are generated at runtime.

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
| Right stick | Aim, independently of where you are moving |
| Space or E | Blast |
| A button or right trigger | Blast |
| Esc, or the pad's Menu button | Pause menu |
| — | The swipe fires automatically on a cooldown |

## Title and pause

The game opens on a title screen — **KAIJU RUMBLE ZERO** over a single **New Game** button,
with the built city frozen behind it. The run is already standing there waiting, so New Game
starts it rather than loading anything. Esc does nothing on the title: there is no run behind
it to go back to.

Esc or the pad's Menu button freezes a run in progress and overlays **Continue** and
**Restart**, with Continue highlighted every time the menu opens. Move between the two with W/S, the arrow keys,
the left stick or the d-pad, and choose with Enter, Space or A. The mouse works too: hovering
highlights, clicking chooses. A cursor left sitting over Restart cannot steal the highlight —
hover only takes over once the mouse has actually moved, and the keys or pad take it straight
back. Esc or Menu again closes the menu, the same as Continue.

Confirm shares its bindings with Blast, so the menu swallows the press that closed it — choosing
Continue with Space or A does not also fire a Blast on the way out.

Pausing is refused while the boss introduction is playing, since that cinematic runs on unscaled
time and would carry on over a frozen world, and after a win or a death, both of which already
own the screen with their own Restart.

Both menus silence effects and voices and leave the music playing. Under the pause menu it
ducks to `pauseMusicVolume` (0.35) so the screen reads as the game waiting; under the title it
stays at full, since the title is the front door rather than an interruption. The pause menu
restores whatever time scale was in force, so pausing during a `[` or `]` speed test does not
quietly reset it.

Restarting — from the pause menu, from the victory banner or with `F10` — goes straight back
into a run rather than stopping at the title, since asking for a restart is already asking for
a run. The Gym scene never shows the title; it has nothing to start.

On a pad the right stick aims: the swipe and the Blast both fire along it, and the
kaiju turns to face it, so you can back away from something while hitting it. Let go
and aim falls back to the direction of travel. A keyboard player is always in that
fallback, so nothing about the keyboard scheme changed.

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
| `F11` | Spawn whoever currently carries power-ups — Commander below size 3, Elite Tank at or above it (`Shift+F11` Mech, `Ctrl+F11` Bruiser, `Ctrl+Shift+F11` Goliath) |
| `F12` | Draw collision footprints |
| `,` `.` | Scrub the wave timeline back 15s / forward 30s |
| `[` `]` | Slow down / speed up time |
| `\` | Reset time scale |

The debug HUD starts **off** — `showDebugHud` in `Tuning.cs` defaults to false, so the game
opens on its title rather than on a wall of diagnostics. `F1` brings it up. The cheat keys
themselves are still live; `enableCheatKeys` in `Tuning.cs` is what switches those off.

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

## City atmosphere and animated effects

The city uses a dark blue nighttime treatment, blooming bright highlights and
slowly drifting, world-anchored blue fog. Dense fog banks have clearer gaps between
them, and luminous details retain their colour so combat and pickups stay readable.
`CityAtmosphereFeature` is installed on both the PC and Mobile URP renderers.
Its **City Atmosphere** settings in `Assets/Settings/PC_Renderer.asset` and
`Mobile_Renderer.asset` control night strength (0.92), bloom strength (0.65) and fog
strength (0.58). These are renderer settings, separate from gameplay `Tuning.cs`.
The haze is a procedural screen effect, not volumetric lighting or physical fog.

Buildings now have animated details layered over their existing artwork:

- Generic pulsing glows and moving luminous motes around detected baked lights.
- Green lightning wrapped around reactor roof coils.
- A fast-moving lava-lamp-style glowing glob underneath laboratory domes.
- Spinning side fans and rotating roof dishes on cantilever buildings.
- Rooftop smokestacks and drifting smoke on civilian buildings.

Building effects follow supported artwork and damage states. Per-building controls
live in `BuildingType.cs`; the generic lighting pass provides a starting point for
further individual building adjustments. The Gym scene is useful for reviewing
these details across the building lineup.

Combat and pickup feedback uses procedural meshes and shaders:

- Standard attacks sweep an energy crescent across their contact area.
- Blast/Prism fires cyan beams with bright cores and soft glow; enemy ranged fire
  uses smaller orange beams. Blast impacts produce small cyan shockwaves.
- Stomp produces a coloured pressure wave with screen displacement. Bruiser shoves,
  Abomination roars, building collision impacts and reactor explosions use expanding
  shockwaves sized to their respective effects.
- Missiles and swarm projectiles have pointed bodies, fins and animated exhaust;
  swarm impacts produce small shockwaves.
- Upgrades are animated double-helix DNA icons retaining the existing colour system.
  Food has a rounded pill silhouette. Hamburger collection relies on pulled food
  rather than an additional collection burst.
- Damage flashes the player's actual sprite red. Building hit debris and enemy
  electrification retain their existing effects.

`StompDistortionFeature` is enabled on both renderer assets. Effect shaders are in
`Assets/KRZ/Resources`; gameplay builds the visual objects at runtime.

## Boss guidance and victory

Once a living boss spawns, a pulsing green triangle at the screen edge points toward
it. The triangle fades out over approximately 0.25 seconds when the boss's position
is inside the camera viewport, then fades back in when it moves outside. This UI
works with the debug HUD disabled.

Defeating the boss allows its available death animation to finish before victory
pauses gameplay. The player cannot take damage during this final delay. The victory
banner and repeating colourful shockwave fireworks use unscaled time, so they
continue while the world is paused. Click **Restart** or press **F10** to start a new
run and restore normal time; victory restart works even with cheat keys disabled.

### Visual verification

The runtime C# sources and effect shader code have passed compilation checks during
this update. Full Unity Play mode visual verification remains pending. Review the
Gym building states, beam and shockwave readability in fog, boss arrow transitions,
and death-animation-to-victory timing in the Game view on the target renderer.
