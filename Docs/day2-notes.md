# Day 2 notes

## First thing

**Wave director and the three-minute timeline.** Nothing currently starts or ends a
run — everything works but it is a sandbox with cheat keys. This is the piece that
makes it a demo.

## Animation bugs to fix (all engine-side, nothing needed from the artist)

1. **Walk only animates when moving vertically.** Horizontal and diagonal stay on a
   single frame. The east-facing walk frames are verified as distinct poses, so this
   is in `UriesArt.LateUpdate`, not the art.
2. **Janky keyframe at the end of a sequence.** Likely the one-shot to idle handoff.
3. **Swipe out of sync with the attack.** Two suspected causes: every hit of a
   Brawler multi-hit burst restarts the animation, so at one stack it retriggers
   every 0.13 s and never reaches its later frames; and damage lands on frame 0
   rather than on the contact frame partway through the swing.

## Enemy variety — agreed, not yet built

**Abomination knockback.** A heavy telegraphed attack that pushes the kaiju away.
Cheap to build on the existing wind-up system. Worth keeping the telegraph long:
knockback that interrupts without warning fights the always-moving feel.

**Dropship — a Tank variant that deploys Grunts.** Replaces the cut Barracks. Better
than the original design: a mobile, killable source of grunt streams rather than a
static building, and "kill the dropship to stop the flow" is a clearer read than
"smash the building". Should slot into the wave director as a spawn unit.

**Rooftop sniper.** Sits on a building, cannot be damaged until that building is
smashed, then becomes vulnerable. **This is the one with architectural weight** —
enemies currently spawn on the ground plane with their own footprint, and this needs
a unit anchored to a building, drawn at roof height, sorting with its host, tracking
the host's destruction, and transitioning to a normal ground enemy. Budget more time
than the other two.

It is also the strongest of the three design-wise: right now the only reason to smash
a building is food, and this gives buildings a tactical reason to exist.

## Open decisions

- **The 102 MB art package is gitignored.** Collaborators cannot pull it. One line to
  change; deliberately not committed because undoing it means rewriting history.
- **Co-op** is nominally in Day 2 slack. Given the director, boss flow, art fixes and
  the protected tuning block, plan on it being cut.

## Audio: the event contract

Sound drops in without touching gameplay code. Every sound the game makes is already
named in `AudioEvents.cs`; gameplay calls `AudioEvents.Play(Sfx.X)` and never touches
an `AudioSource`. Deliver one clip per event name below.

| Event | Fires when |
| --- | --- |
| `Footstep` | Reserved, not yet called |
| `BuildingHit` | Any hit on a building |
| `BuildingDestroyed` | A building collapses to rubble |
| `FoodPickup` | Food reaches the kaiju |
| `Swipe` | The auto-attack swings |
| `SwipeHit` | A swipe connects with something |
| `Blast` | The special fires |
| `GrowTier` | Reaching a new size |
| `Shrink` | Losing a size on death |
| `PlayerHit` | The kaiju takes damage |
| `Squish` | An outgrown enemy dies underfoot |
| `UpgradePickup` | A power-up is collected |
| `EnemySpawn` | An enemy appears, and a Mech missile volley |
| `EnemyDeath` | An enemy is killed |
| `BossArrive` | Reserved for the boss arrival beat |
| `Win` | Reserved |
| `Lose` | Game over at size 1 |

Notes for whoever is making these:

- **Frequency matters more than length.** `Swipe` fires every couple of seconds all
  run and `FoodPickup` fires dozens of times a minute — both need to survive heavy
  repetition. `GrowTier`, `Lose` and `BossArrive` fire once or twice a run and can
  be much bigger.
- Short, dry, and punchy beats long and reverberant. The mix will be busy.
- `AudioEvents.logEvents = true` prints every event as it fires, so timing can be
  checked before any audio exists.
