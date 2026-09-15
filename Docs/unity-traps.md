# Unity traps

Failures that cost real time on Kaiju Rumble Zero, written down so the next project loses
an afternoon to something new instead.

Every entry is here because it **failed silently** — no exception, nothing red in the console,
often nothing in the console at all. That is the common thread and the reason this file exists:
a loud failure teaches you itself. These don't.

Organised by **symptom**, because that is what you have when it happens.

---

## Editing a value in C# changes nothing at runtime

**Symptom.** You change a field's default in a `ScriptableObject` subclass. The game keeps using
the old value. No error. Recompiling doesn't help. The `.asset` file on disk doesn't even mention
the field, so it looks like the C# default must be winning — and it isn't.

**Cause.** Unity serves a `ScriptableObject` asset's *cached serialized state*, which lives in
`Library/`, not in the `.asset` YAML you can read. Once a field has been serialized into that
cache, its value wins over the C# field initialiser permanently.

**How it actually went wrong.** Three times in one session.

- A `displayScale` set to `3f` in source; the asset kept returning `1`, through four asset bakes,
  a recompile and a reimport. Two debugging rounds were spent measuring screenshots and reading
  transform code, because the `.asset` file looked clean and was therefore ruled out. That check
  proved nothing — the stale copy was never in that file.
- A new enemy type added to the config array simply wasn't there at runtime. The wave that wanted
  it fired exactly on schedule and logged `wants enemy type 'X', which does not exist`. The level
  logic was perfect; the data never arrived.

**Diagnosis.** One line settles it. Log the loaded asset's value beside one built fresh from the
initialiser:

```csharp
var fresh = ScriptableObject.CreateInstance<Tuning>();
Debug.Log($"asset [{Describe(tuning)}]   fresh [{Describe(fresh)}]");
```

Disagreement means the cache is winning. Agreement means the compiled assembly isn't your source.
Either answer points somewhere different, and both beat guessing.

**Fix.** Split the config by whether a human tweaks it *while the game is running*.

- **Numbers you tune live** — damage, speeds, cooldowns — stay on the asset. That is what it is
  for, and the risk is worth the Inspector.
- **Wiring** — asset paths, table entries, level definitions, anything an enum switches on —
  belongs in a `static readonly` table in code, where nothing can shadow it.

Keep the asset holding as close to nothing as you can manage, and write down what it is allowed
to hold. Then a diff on it is a red flag rather than noise.

---

## A render-to-texture readback is a solid opaque square

**Symptom.** You render a camera to a `RenderTexture` with a transparent clear colour, read the
pixels back, and every pixel has alpha 1. The image is correct; the transparency is gone.

**Cause.** URP routinely forces alpha to 1 in its final blit. Nothing warns you.

**Fix.** Don't ask for the alpha channel. Render the same frame twice, over black and over white,
and solve for coverage — it is immune to whatever the pipeline did:

```
a   = 1 - (white - black)
rgb = black / a
```

Costs a second render and recovers antialiased edges as a bonus.

---

## Captured images carry the scene's post-processing

**Symptom.** Sprites baked from a camera come out subtly tinted, foggy or bloomed. They look
*almost* right, which is the dangerous part — you can ship them and notice weeks later.

**Cause.** `ScriptableRendererFeature`s typically early-out on `cameraType != CameraType.Game`,
and a plain `Camera` you create in code **is** `CameraType.Game` by default. So your capture
camera runs the full game post stack.

**Fix.** `cam.cameraType = CameraType.Preview;` before rendering.

---

## Spawn placement silently starts failing

**Symptom.** Objects intermittently refuse to spawn. No error. On this project the boss simply
went missing, and the cause was two screens away.

**Cause.** `Physics2D.queriesHitTriggers` is global and defaults to true, so every plain
`OverlapCircle` clearance check also sees trigger colliders. A projectile drifting through a
spawn point makes that point look occupied.

**Fix.** Set `Physics2D.queriesHitTriggers = false` once at startup and pass an explicit
`ContactFilter2D` with `useTriggers = true` where you *do* want triggers. Then the default is the
safe one and the exceptions are visible at the call site.

---

## Sprites have a dark halo when scaled or filtered

**Symptom.** A pale sprite picks up a dark rim in motion or at small sizes. Worse on bright art;
nearly invisible on dark art, which is how it survives review.

**Cause.** Straight alpha with black in the transparent pixels. With `alphaIsTransparency` off,
no colour is bled outward under the matte, so filtering samples the black underneath.

**Fix.** `alphaIsTransparency = true` on import — an `AssetPostprocessor` beats remembering.
If you generate or downsample images yourself, weight the colour average by alpha, or you
reintroduce exactly the same artefact by hand.

---

## A component's initialisation silently never runs

**Symptom.** You `AddComponent`, then assign its fields, and its setup never takes effect. No
null reference — it just quietly does nothing.

**Cause.** `AddComponent` runs `Awake` **synchronously, before** the calling line returns. So
`Awake` sees every field at its default. Worse, if `Awake` caches "what I've applied", the later
assignment looks like no change at all and is skipped forever.

**Fix.** Do real initialisation on the first `Update`/`LateUpdate`, gated by an explicit
`applied` flag rather than by comparing state — the comparison is exactly what lies.

---

## A posed rig measures as its bind pose

**Symptom.** You `SampleAnimation` a model and read `Renderer.bounds` to measure it. The numbers
never change between poses.

**Cause.** `SkinnedMeshRenderer.bounds` is the bind-pose box and does not follow sampling.

**Fix.** `smr.BakeMesh(mesh)` and read the baked mesh's bounds.

---

## Every sampled animation frame comes out identical

**Symptom.** You sample a clip at increasing times and get the same pose each time.

**Cause.** An enabled `Animator` re-evaluates its controller and overwrites the pose
`SampleAnimation` just set.

**Fix.** Disable every `Animator` in the hierarchy before sampling.

---

## Auto-framing reserves far too much space

**Symptom.** Something fitted to its own bounding box occupies a small fraction of the frame,
while a different object framed the same way fills it.

**Cause.** An axis-aligned bounding box around a long, low object balloons as the object rotates —
its corners sweep the half-diagonal. The box is honest; it just isn't the silhouette.

**How it actually went wrong.** A quadruped baked at **16%** of its canvas where the reference art
filled **80%** — a 5× error — because the framing pass sized to a box the model never filled.
Fixing the box maths got it to 29%. Only measuring *rendered pixels* got it right.

**Fix.** For anything where framing matters, do a cheap low-resolution render pass and measure the
pixels that actually got drawn. Bounding boxes are for culling, not composition.

---

## Merge conflicts in scenes and prefabs are unresolvable

**Symptom.** Two people touch a scene; git produces YAML that opens as a corrupt scene.

**Fix.** Mark Unity YAML `-merge` in `.gitattributes` so git refuses rather than guessing:

```
*.unity   -merge
*.prefab  -merge
*.asset   -merge
```

Then resolve by hand or regenerate. The deeper fix is to build from code where you can, so there
is little scene to conflict over.

---

## Tooling notes

**A `.unitypackage` is a gzipped tar.** You can list and extract it without importing — each entry
is a GUID folder holding `asset`, `asset.meta` and `pathname`. Useful for seeing what a package
contains, and for reading clip definitions out of an FBX's `.meta` before committing to an import.

**GNU tar on Windows** reads `C:/...` as a remote host and fails with `Cannot connect to C:`.
Pass `--force-local`.

**Asset Store packages import to their embedded paths.** You can deselect files but you cannot
redirect them, so plan for where they land rather than where you want them.

**Unity writes its log inside the project** once a project is open:
`Logs/Editor.log`, not the global `%LOCALAPPDATA%` one. `grep "error CS"` on it beats asking a
human to copy errors out of the console, and `Debug.Log` output lands there too — which makes
"add a log, run once, read the log" a faster loop than reasoning about what might be happening.

---

## The meta-lesson

Every entry above failed **quietly**. The debugging move that worked, repeatedly, was to stop
reasoning about what *should* be happening and make the program state its own case — log the
value, measure the pixels, diff the loaded object against a fresh one.

The corollary is about trust: when a check comes back clean, ask whether it could have come back
clean anyway. Reading the `.asset` file and finding it tidy felt like ruling out serialization.
It ruled out nothing, and cost two rounds of looking in the wrong place.
