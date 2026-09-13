using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Renders a 3D character rig out to the flat directional sprite sequences the game
/// actually loads. The Uries package arrived pre-rendered this way; the Asset Store
/// rigs did not, so this is the missing step that puts them on the same footing.
///
/// Output matches the Uries path template exactly —
/// {root}/{clip}/{dir}/{prefix}_{clip}_{dir}_{frame} — so a baked character needs no
/// new loader, just an ArtConfig pointing at it. The one difference is that Uries has
/// a Level_N folder per growth tier and these do not: one bake serves all five tiers
/// and PlayerController.SetScale grows it.
///
/// Nothing here writes outside Assets/KRZ/Resources/{character}/. The source packages
/// are read-only to this script — materials are swapped on a throwaway instance, not
/// on the assets.
/// </summary>
public static class CharacterSpriteBaker
{
    // ---- art spec (matches UriesArt / DirectionalArt) ------------------------

    const int FrameSize = 512;
    const int Supersample = 2;       // render at 2x and box-filter down; there is no MSAA here
    const float PivotY = 0.12f;      // DirectionalArt.Pivot.y — the ground contact point
    const float Padding = 0.04f;     // breathing room so a swing does not clip the frame

    /// <summary>
    /// atan(0.5) in degrees. The world is a 2:1 projection — one unit of depth reads as
    /// half a unit of screen height — and that ratio is a camera elevation, not a style
    /// choice. Get this wrong and baked sprites sit at a different angle from the
    /// buildings they walk behind.
    /// </summary>
    const float IsoPitch = 26.565f;

    /// <summary>
    /// The five rendered directions. West is mirrored at runtime (DirectionalArt.MirrorSource),
    /// so baking it would be 60% more files for nothing.
    /// </summary>
    static readonly string[] Directions = { "south", "southeast", "east", "northeast", "north" };

    /// <summary>
    /// Yaw applied to the rig per direction, before baseYaw. South is "facing the camera".
    /// </summary>
    static readonly float[] DirectionYaw = { 0f, 45f, 90f, 135f, 180f };

    /// <summary>
    /// Which way to turn the rig for a direction.
    ///
    /// Negating the yaw leaves south and north where they are — 0 and 180 are their own
    /// opposites — and swaps the three diagonals for their western twins. So this is exactly
    /// the correction for a rig that comes out facing the right way up and down the screen
    /// but mirrored left to right, which is what a vendor modelling the character to face
    /// -Z rather than +Z produces. The Skeleton needs it and the Lizard does not.
    ///
    /// Every pass turns the rig through here rather than composing the angle itself, so the
    /// survey that measures the framing and the render that writes the frames cannot disagree
    /// about which way the character was pointing.
    /// </summary>
    static Quaternion Yaw(CharacterSpec spec, int direction)
        => Quaternion.Euler(0f, spec.baseYaw + DirectionYaw[direction] * (spec.mirrorYaw ? -1f : 1f), 0f);

    // ---- what to bake -------------------------------------------------------

    struct ClipSpec
    {
        public string clip;      // game-side clip name: the folder it lands in
        public string fbx;       // asset path holding the AnimationClip
        public string clipName;  // clip name inside that asset; null takes the first one
        public int frames;
        public bool loop;        // loops sample [0, length), one-shots sample [0, length]
    }

    class CharacterSpec
    {
        public string name;        // Resources folder, e.g. "Lizard"
        public string prefix;      // filename stem, e.g. "lizard"
        public string prefabPath;
        public float baseYaw;      // per-rig correction; see DirectionYaw
        public bool mirrorYaw;     // set when the rig's east and west come out swapped; see Yaw
        public ClipSpec[] clips;
    }

    // Frame counts match UriesArt's table (idle 4, walk 8, swipe 6, blast 6, hit 3) so a
    // baked character drops into the same playback timing at 12 fps.
    static readonly CharacterSpec[] Characters =
    {
        new CharacterSpec
        {
            name = "Lizard",
            prefix = "lizard",
            prefabPath = "Assets/Hatogame_new/Lizard/Perfabs/Lizard_A.prefab",
            baseYaw = 0f,
            clips = new[]
            {
                new ClipSpec { clip = "idle",  fbx = "Assets/Hatogame_new/Lizard/Animation/Lizard@idle.FBX",    frames = 4, loop = true },
                new ClipSpec { clip = "walk",  fbx = "Assets/Hatogame_new/Lizard/Animation/Lizard@walk.FBX",    frames = 8, loop = true },
                new ClipSpec { clip = "swipe", fbx = "Assets/Hatogame_new/Lizard/Animation/Lizard@attack1.FBX", frames = 6 },
                new ClipSpec { clip = "blast", fbx = "Assets/Hatogame_new/Lizard/Animation/Lizard@attack2.FBX", frames = 6 },
                new ClipSpec { clip = "hit",   fbx = "Assets/Hatogame_new/Lizard/Animation/Lizard@hit.FBX",     frames = 3 },
            },
        },
        new CharacterSpec
        {
            name = "Skeleton",
            prefix = "skeleton",
            prefabPath = "Assets/Skeleton/Prefabs/Skeleton.prefab",
            baseYaw = 0f,

            // This rig faces the opposite way from the Lizard's, so its first bake walked
            // east when the player pushed west.
            mirrorYaw = true,
            // One FBX with seven clip splits, so every entry names the same file and
            // picks a clip out of it by name.
            clips = new[]
            {
                new ClipSpec { clip = "idle",  fbx = "Assets/Skeleton/Model/Skeleton.FBX", clipName = "Skeleton_idle",   frames = 4, loop = true },
                new ClipSpec { clip = "walk",  fbx = "Assets/Skeleton/Model/Skeleton.FBX", clipName = "Skeleton_walk",   frames = 8, loop = true },
                new ClipSpec { clip = "swipe", fbx = "Assets/Skeleton/Model/Skeleton.FBX", clipName = "Skeleton_attack", frames = 6 },
                new ClipSpec { clip = "blast", fbx = "Assets/Skeleton/Model/Skeleton.FBX", clipName = "Skeleton_cast",   frames = 6 },
                new ClipSpec { clip = "hit",   fbx = "Assets/Skeleton/Model/Skeleton.FBX", clipName = "Skeleton_damage", frames = 3 },
            },
        },
    };

    // ---- menu ---------------------------------------------------------------

    [MenuItem("KRZ/Art/Bake Character Sprites")]
    static void BakeAll()
    {
        try
        {
            foreach (var spec in Characters) Bake(spec, contactSheetOnly: false);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    /// <summary>
    /// One PNG per character showing the idle pose in all five directions, written beside
    /// the project rather than into Resources. Cheap way to check framing and facing before
    /// committing to the full run — a wrong baseYaw is obvious here and costs seconds
    /// instead of a few hundred wasted frames.
    /// </summary>
    [MenuItem("KRZ/Art/Bake Contact Sheet (preview)")]
    static void BakeContactSheets()
    {
        try
        {
            foreach (var spec in Characters) Bake(spec, contactSheetOnly: true);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
            AssetDatabase.Refresh();
        }
    }

    // ---- bake ---------------------------------------------------------------

    static void Bake(CharacterSpec spec, bool contactSheetOnly)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(spec.prefabPath);
        if (prefab == null)
        {
            Debug.LogError($"KRZ bake: no prefab at {spec.prefabPath}. Import the package first.");
            return;
        }

        var clips = new List<(ClipSpec spec, AnimationClip clip)>();
        foreach (var cs in spec.clips)
        {
            var clip = FindClip(cs.fbx, cs.clipName);
            if (clip == null)
            {
                Debug.LogError($"KRZ bake: {spec.name} — clip '{cs.clipName ?? "(first)"}' not found in {cs.fbx}.");
                return;
            }
            clips.Add((cs, clip));
        }

        var scene = EditorSceneManager.NewPreviewScene();
        GameObject instance = null;
        Mesh scratch = null;

        try
        {
            instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
            instance.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity);
            instance.transform.localScale = Vector3.one;

            // An enabled Animator re-evaluates its controller and overwrites whatever
            // SampleAnimation just posed, which reads as "every frame came out identical".
            foreach (var a in instance.GetComponentsInChildren<Animator>()) a.enabled = false;

            SwapToUrpMaterials(instance);
            scratch = new Mesh { name = "KRZ bake scratch" };

            var camRot = Quaternion.Euler(IsoPitch, 0f, 0f);
            var cam = BuildCamera(scene, camRot);
            BuildLights(scene);

            // Framing is measured once for the whole character and then held. Per-frame
            // auto-fit would make the kaiju breathe in and out between frames.
            //
            // It takes two passes because bounding boxes are not good enough. An axis-aligned
            // box around a long, low rig balloons as the rig turns — its corners sweep the
            // half-diagonal — and sizing the frame to that reserved room for a box the lizard
            // never filled: it baked at 16% of the canvas against the delivered art's 80%.
            // So the box is only used to guarantee the rig is on screen at all, and the
            // framing that matters comes from the pixels that actually got drawn.
            float coarse = CoarseRadius(instance, spec, clips, camRot, scratch);
            if (coarse <= 0.0001f)
            {
                Debug.LogError($"KRZ bake: {spec.name} measured as empty — no readable renderers on the prefab.");
                return;
            }

            cam.orthographicSize = coarse;
            cam.transform.SetPositionAndRotation(camRot * Vector3.forward * -100f, camRot);

            var seen = MeasurePixels(spec, instance, cam, clips, coarse);
            if (!seen.any)
            {
                Debug.LogError($"KRZ bake: {spec.name} rendered nothing — the rig is off camera or its materials did not survive the URP swap.");
                return;
            }

            float height = Mathf.Max((seen.highest - seen.lowest) / (1f - PivotY),
                                     seen.halfWidth * 2f) * (1f + Padding);

            cam.orthographicSize = height * 0.5f;

            // Worth reading in the console: the delivered Uries frames fill 80% of their
            // canvas, so anything far off that will look wrong next to the Alien in game.
            Debug.Log($"KRZ bake: {spec.name} fills {(seen.highest - seen.lowest) / height:P0} " +
                      $"of the frame ({FrameSize}px canvas).");

            // Sit the lowest drawn pixel on the pivot line rather than the rig's origin.
            // That is the convention the delivered Uries frames use — its feet are its
            // lowest pixels and they land at 0.119 — and it is what keeps a quadruped,
            // whose origin sits up inside its body, from being shrunk to make room
            // underneath itself.
            Vector3 aim = camRot * Vector3.up * (seen.lowest + (0.5f - PivotY) * height);
            cam.transform.SetPositionAndRotation(aim - camRot * Vector3.forward * 100f, camRot);

            if (contactSheetOnly) WriteContactSheet(spec, instance, cam, clips[0]);
            else WriteFrames(spec, instance, cam, clips);
        }
        finally
        {
            if (scratch != null) Object.DestroyImmediate(scratch);
            if (instance != null) Object.DestroyImmediate(instance);
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    static Camera BuildCamera(Scene scene, Quaternion rot)
    {
        var go = new GameObject("~BakeCamera");
        SceneManager.MoveGameObjectToScene(go, scene);
        var cam = go.AddComponent<Camera>();

        // CityAtmosphereFeature and StompDistortionFeature both early-out on anything that
        // is not CameraType.Game. Leaving this as Game bakes the city's night tint, fog and
        // bloom into every sprite — which looks almost right, and is very hard to spot later.
        cam.cameraType = CameraType.Preview;

        cam.orthographic = true;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        cam.allowHDR = false;
        cam.allowMSAA = false;
        cam.enabled = false;          // driven by explicit Render() calls
        cam.scene = scene;            // confine culling to the preview scene
        cam.transform.rotation = rot;
        return cam;
    }

    static void BuildLights(Scene scene)
    {
        // Two directional lights rather than an ambient term: RenderSettings is global and
        // writing it here would dirty the open scene's lighting.
        Add("~BakeKey", new Vector3(50f, -30f, 0f), 1.25f, new Color(1f, 0.97f, 0.92f));
        Add("~BakeFill", new Vector3(20f, 150f, 0f), 0.45f, new Color(0.75f, 0.82f, 1f));

        void Add(string name, Vector3 euler, float intensity, Color color)
        {
            var go = new GameObject(name);
            SceneManager.MoveGameObjectToScene(go, scene);
            go.transform.rotation = Quaternion.Euler(euler);
            var l = go.AddComponent<Light>();
            l.type = LightType.Directional;
            l.intensity = intensity;
            l.color = color;
            l.shadows = LightShadows.None;
        }
    }

    // ---- measuring ----------------------------------------------------------

    /// <summary>
    /// Camera-space extents of the drawn pixels, relative to the rig's origin, in world units.
    /// lowest and highest are signed: a rig whose lowest pixel sits above its origin — which a
    /// hovering or rearing pose can produce — reports a positive lowest, and the framing still
    /// works out.
    /// </summary>
    struct Extents { public float lowest, highest, halfWidth; public bool any; }

    /// <summary>Enough to survey the pose at; a character is a few hundred pixels of silhouette.</summary>
    const int MeasureSize = 192;

    /// <summary>
    /// A generous half-height from the rig's bounds, used only to guarantee the survey pass can
    /// see the whole character. Overestimating here is free — it costs resolution in a render
    /// nothing keeps — while underestimating would clip the very extreme the survey is looking for.
    /// </summary>
    static float CoarseRadius(GameObject instance, CharacterSpec spec,
        List<(ClipSpec spec, AnimationClip clip)> clips, Quaternion camRot, Mesh scratch)
    {
        var inv = Quaternion.Inverse(camRot);
        float r = 0f;

        foreach (var (cs, clip) in clips)
            for (int d = 0; d < Directions.Length; d++)
            {
                instance.transform.rotation = Yaw(spec, d);
                for (int f = 0; f < cs.frames; f++)
                {
                    clip.SampleAnimation(instance, SampleTime(cs, clip, f));
                    foreach (var corner in WorldCorners(instance, scratch))
                    {
                        var c = inv * corner;
                        r = Mathf.Max(r, Mathf.Abs(c.x), Mathf.Abs(c.y));
                    }
                }
            }

        return r * 1.25f;
    }

    /// <summary>
    /// Renders every pose small and reads back where the character actually landed. This is the
    /// framing that counts: it sees the silhouette rather than a box around the skeleton, so a
    /// long rig is measured by what it draws instead of by how far its bones reach.
    /// </summary>
    static Extents MeasurePixels(CharacterSpec spec, GameObject instance, Camera cam,
        List<(ClipSpec spec, AnimationClip clip)> clips, float orthoSize)
    {
        var e = new Extents { lowest = float.MaxValue, highest = float.MinValue };
        float perPixel = orthoSize * 2f / MeasureSize;
        int done = 0, total = 0;
        foreach (var (cs, _) in clips) total += cs.frames * Directions.Length;

        foreach (var (cs, clip) in clips)
            for (int d = 0; d < Directions.Length; d++)
            {
                instance.transform.rotation = Yaw(spec, d);
                for (int f = 0; f < cs.frames; f++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar($"Framing {spec.name}",
                            $"{cs.clip} / {Directions[d]} / {f}", done / (float)total))
                        return e;

                    clip.SampleAnimation(instance, SampleTime(cs, clip, f));
                    var px = Solve(cam, MeasureSize);

                    for (int y = 0; y < MeasureSize; y++)
                        for (int x = 0; x < MeasureSize; x++)
                        {
                            // Ignore the faintest edge pixels: antialiasing spreads a trace of
                            // coverage a pixel or two past the silhouette, and measuring to that
                            // would pad every character by the same invisible margin.
                            if (px[y * MeasureSize + x].a <= 0.06f) continue;

                            e.any = true;
                            float dx = (x + 0.5f - MeasureSize * 0.5f) * perPixel;
                            float dy = (y + 0.5f - MeasureSize * 0.5f) * perPixel;
                            e.halfWidth = Mathf.Max(e.halfWidth, Mathf.Abs(dx));
                            e.lowest = Mathf.Min(e.lowest, dy);
                            e.highest = Mathf.Max(e.highest, dy);
                        }
                    done++;
                }
            }

        return e;
    }

    static float SampleTime(ClipSpec cs, AnimationClip clip, int frame)
    {
        // A looping clip whose last sample equals its first would stutter, so loops stop one
        // step short of the end; one-shots run right to the final pose.
        float span = cs.frames <= 1 ? 1f : (cs.loop ? cs.frames : cs.frames - 1);
        return clip.length * (frame / span);
    }

    static IEnumerable<Vector3> WorldCorners(GameObject instance, Mesh scratch)
    {
        foreach (var r in instance.GetComponentsInChildren<Renderer>())
        {
            if (!r.enabled) continue;

            Bounds local;
            Matrix4x4 toWorld;
            if (r is SkinnedMeshRenderer smr)
            {
                // Renderer.bounds on a skinned mesh is the bind-pose box and does not follow
                // SampleAnimation, so the pose has to be baked out to read it.
                if (smr.sharedMesh == null) continue;
                smr.BakeMesh(scratch, false);
                local = scratch.bounds;
                toWorld = smr.localToWorldMatrix;
            }
            else if (r is MeshRenderer && r.TryGetComponent<MeshFilter>(out var mf) && mf.sharedMesh != null)
            {
                local = mf.sharedMesh.bounds;
                toWorld = r.localToWorldMatrix;
            }
            else continue;

            Vector3 c = local.center, x = local.extents;
            for (int i = 0; i < 8; i++)
                yield return toWorld.MultiplyPoint3x4(c + new Vector3(
                    (i & 1) == 0 ? -x.x : x.x,
                    (i & 2) == 0 ? -x.y : x.y,
                    (i & 4) == 0 ? -x.z : x.z));
        }
    }

    // ---- writing ------------------------------------------------------------

    static void WriteFrames(CharacterSpec spec, GameObject instance, Camera cam,
        List<(ClipSpec spec, AnimationClip clip)> clips)
    {
        string root = $"Assets/KRZ/Resources/{spec.name}";
        int total = 0, done = 0;
        foreach (var (cs, _) in clips) total += cs.frames * Directions.Length;

        foreach (var (cs, clip) in clips)
            for (int d = 0; d < Directions.Length; d++)
            {
                string dir = $"{root}/{cs.clip}/{Directions[d]}";
                Directory.CreateDirectory(dir);
                instance.transform.rotation = Yaw(spec, d);

                for (int f = 0; f < cs.frames; f++)
                {
                    if (EditorUtility.DisplayCancelableProgressBar($"Baking {spec.name}",
                            $"{cs.clip} / {Directions[d]} / {f}", done / (float)total))
                        return;

                    clip.SampleAnimation(instance, SampleTime(cs, clip, f));
                    var tex = Capture(cam);
                    File.WriteAllBytes($"{dir}/{spec.prefix}_{cs.clip}_{Directions[d]}_{f:00}.png", tex.EncodeToPNG());
                    Object.DestroyImmediate(tex);
                    done++;
                }
            }

        Debug.Log($"KRZ bake: wrote {done} frames to {root} at {FrameSize}px, pivot (0.5, {PivotY}).");
    }

    static void WriteContactSheet(CharacterSpec spec, GameObject instance, Camera cam,
        (ClipSpec spec, AnimationClip clip) idle)
    {
        var sheet = new Texture2D(FrameSize * Directions.Length, FrameSize, TextureFormat.RGBA32, false);
        for (int d = 0; d < Directions.Length; d++)
        {
            instance.transform.rotation = Yaw(spec, d);
            idle.clip.SampleAnimation(instance, 0f);
            var tex = Capture(cam);
            sheet.SetPixels(d * FrameSize, 0, FrameSize, FrameSize, tex.GetPixels());
            Object.DestroyImmediate(tex);
        }
        sheet.Apply();

        // Outside Assets/ on purpose: this is a throwaway check, not art, and writing it into
        // Resources would ship it in the build.
        string path = Path.Combine(Path.GetDirectoryName(Application.dataPath), $"bake_preview_{spec.name}.png");
        File.WriteAllBytes(path, sheet.EncodeToPNG());
        Object.DestroyImmediate(sheet);
        Debug.Log($"KRZ bake: contact sheet at {path} — order is {string.Join(", ", Directions)}.");
    }

    /// <summary>
    /// Renders one frame twice, on black and on white, and solves for coverage.
    ///
    /// The obvious approach — clear to transparent and read the alpha channel — does not
    /// survive URP, which routinely forces alpha to 1 in the final blit and hands back a
    /// solid square. Compositing the same pixel over two known backgrounds gives
    /// a = 1 - (white - black) regardless of what the pipeline did to the alpha channel,
    /// and recovers antialiased edges as a bonus.
    /// </summary>
    static Color[] Solve(Camera cam, int size)
    {
        var rt = RenderTexture.GetTemporary(size, size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
        var prev = RenderTexture.active;
        cam.targetTexture = rt;

        Color[] onBlack = Read(Color.black);
        Color[] onWhite = Read(Color.white);

        cam.targetTexture = null;
        RenderTexture.active = prev;
        RenderTexture.ReleaseTemporary(rt);

        bool linear = QualitySettings.activeColorSpace == ColorSpace.Linear;
        var solved = new Color[onBlack.Length];
        for (int i = 0; i < solved.Length; i++)
        {
            Color b = linear ? onBlack[i].linear : onBlack[i];
            Color w = linear ? onWhite[i].linear : onWhite[i];

            float a = Mathf.Clamp01(1f - ((w.r - b.r) + (w.g - b.g) + (w.b - b.b)) / 3f);
            if (a <= 0.0001f) { solved[i] = Color.clear; continue; }

            var rgb = new Color(b.r / a, b.g / a, b.b / a, 1f);
            if (linear) rgb = rgb.gamma;
            solved[i] = new Color(rgb.r, rgb.g, rgb.b, a);
        }

        return solved;

        Color[] Read(Color bg)
        {
            cam.backgroundColor = bg;
            cam.Render();
            RenderTexture.active = rt;
            var t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            t.Apply();
            var px = t.GetPixels();
            Object.DestroyImmediate(t);
            return px;
        }
    }

    /// <summary>One finished frame: solved at Supersample times the output size, then filtered down.</summary>
    static Texture2D Capture(Camera cam)
    {
        int big = FrameSize * Supersample;
        return Downsample(Solve(cam, big), big);
    }

    /// <summary>
    /// Box-filters the supersampled frame down, averaging colour weighted by alpha.
    /// A plain average would pull the transparent pixels' colour into the edge and draw a
    /// dark rim — the same artefact KrzTextureImporter's alphaIsTransparency guards against
    /// on the way in.
    /// </summary>
    static Texture2D Downsample(Color[] src, int srcSize)
    {
        int n = Supersample;
        var outTex = new Texture2D(FrameSize, FrameSize, TextureFormat.RGBA32, false);
        var dst = new Color[FrameSize * FrameSize];

        for (int y = 0; y < FrameSize; y++)
            for (int x = 0; x < FrameSize; x++)
            {
                float r = 0f, g = 0f, b = 0f, a = 0f;
                for (int sy = 0; sy < n; sy++)
                    for (int sx = 0; sx < n; sx++)
                    {
                        var c = src[(y * n + sy) * srcSize + (x * n + sx)];
                        r += c.r * c.a; g += c.g * c.a; b += c.b * c.a; a += c.a;
                    }
                dst[y * FrameSize + x] = a > 0.0001f
                    ? new Color(r / a, g / a, b / a, a / (n * n))
                    : Color.clear;
            }

        outTex.SetPixels(dst);
        outTex.Apply();
        return outTex;
    }

    // ---- source assets ------------------------------------------------------

    static AnimationClip FindClip(string assetPath, string clipName)
    {
        AnimationClip first = null;
        foreach (var o in AssetDatabase.LoadAllAssetsAtPath(assetPath))
        {
            if (o is not AnimationClip c) continue;
            // Unity generates a hidden __preview__ clip alongside the real ones.
            if (c.name.StartsWith("__preview__")) continue;
            if (clipName != null && c.name == clipName) return c;
            first ??= c;
        }
        return clipName == null ? first : null;
    }

    /// <summary>
    /// Asset Store packages of this vintage ship Built-in Standard materials, which render
    /// magenta under URP. Rebuilds each one as URP/Lit on the throwaway instance, carrying
    /// the albedo and normal across. The package's own .mat files are never touched.
    /// </summary>
    static void SwapToUrpMaterials(GameObject instance)
    {
        var lit = Shader.Find("Universal Render Pipeline/Lit");
        if (lit == null) return;

        foreach (var r in instance.GetComponentsInChildren<Renderer>())
        {
            var swapped = new Material[r.sharedMaterials.Length];
            for (int i = 0; i < swapped.Length; i++)
            {
                var src = r.sharedMaterials[i];
                if (src == null) { swapped[i] = null; continue; }
                if (src.shader == lit) { swapped[i] = src; continue; }

                var m = new Material(lit) { hideFlags = HideFlags.HideAndDontSave };
                if (FirstTexture(src, "_BaseMap", "_MainTex") is Texture albedo) m.SetTexture("_BaseMap", albedo);
                if (FirstTexture(src, "_BumpMap", "_NormalMap") is Texture normal)
                {
                    m.SetTexture("_BumpMap", normal);
                    m.EnableKeyword("_NORMALMAP");
                }
                m.SetFloat("_Smoothness", 0.15f);   // these rigs are matte; stock 0.5 reads as wet plastic
                swapped[i] = m;
            }
            r.sharedMaterials = swapped;
        }
    }

    static Texture FirstTexture(Material m, params string[] names)
    {
        foreach (var n in names)
            if (m.HasProperty(n) && m.GetTexture(n) != null) return m.GetTexture(n);
        return null;
    }
}
