using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Dead-zone follow camera. The player moves freely inside a central box; once they
/// push past it the camera pans to keep up. Zoom pulls back as the player grows, at
/// (scale ^ zoomExponent) — square root by default, so you visibly dominate more of
/// the frame without losing sight of what is coming at you.
/// </summary>
public class CameraRig : MonoBehaviour
{
    static CameraRig introOwner;
    public static bool IsBossIntroductionPlaying => introOwner != null && introOwner.introActive;
    readonly Queue<Enemy> introductions = new();
    bool introActive;
    float savedZoom;

    public Tuning tuning;
    public Transform target;

    Camera cam;
    Vector3 vel;
    float shakeAmount;
    float shakeLeft;
    float shakeTotal;

    public void Shake(float amount, float duration)
    {
        shakeAmount = Mathf.Max(shakeAmount, amount);
        shakeTotal = duration;
        shakeLeft = duration;
    }

    void Awake()
    {
        cam = GetComponent<Camera>();
        cam.orthographic = true;

        // Sprites sort by their pivot's Y: higher on screen draws first, so nearer
        // things cover it. This is the whole "walk behind the tower" effect.
        cam.transparencySortMode = TransparencySortMode.CustomAxis;
        cam.transparencySortAxis = new Vector3(0f, 1f, 0f);
    }

    void LateUpdate()
    {
        if (introActive) return;
        if (target == null) return;

        float scale = 1f;
        var player = target.GetComponent<PlayerController>();
        if (player != null) scale = player.Scale;

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            tuning.baseOrthoSize * Mathf.Pow(scale, tuning.zoomExponent),
            1f - Mathf.Exp(-6f * Time.deltaTime));

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        // The camera always wants the player centred. All of the trailing comes from
        // the smoothing, not from a dead zone: a dead zone lets the player sit out at
        // the margins for as long as they keep walking, which is what we do not want.
        var want = new Vector3(target.position.x, target.position.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, want, ref vel, tuning.followLag);

        // Hard containment. Smoothing alone lets a fast kaiju drift further out the
        // faster it moves, so this caps how far from centre the player can ever be.
        float maxX = halfW * tuning.maxPlayerOffset;
        float maxY = halfH * tuning.maxPlayerOffset;
        Vector2 off = (Vector2)target.position - (Vector2)transform.position;

        Vector3 clamped = transform.position;
        if (Mathf.Abs(off.x) > maxX) clamped.x = target.position.x - Mathf.Sign(off.x) * maxX;
        if (Mathf.Abs(off.y) > maxY) clamped.y = target.position.y - Mathf.Sign(off.y) * maxY;
        transform.position = clamped;

        // Shake rides on top of the settled position so it never fights the follow.
        if (shakeLeft > 0f)
        {
            shakeLeft -= Time.deltaTime;
            float falloff = Mathf.Clamp01(shakeLeft / Mathf.Max(0.01f, shakeTotal));
            float amp = shakeAmount * falloff * falloff;
            transform.position += new Vector3(Random.Range(-amp, amp), Random.Range(-amp, amp) * 0.6f, 0f);
            if (shakeLeft <= 0f) shakeAmount = 0f;
        }
    }

    public void IntroduceBoss(Enemy boss)
    {
        if (boss == null || target == null || !isActiveAndEnabled) return;
        introductions.Enqueue(boss);
        if (!introActive) StartCoroutine(ShowIntroductions());
    }

    IEnumerator ShowIntroductions()
    {
        introActive = true;
        introOwner = this;
        savedZoom = cam.orthographicSize;
        shakeAmount = shakeLeft = 0;
        try
        {
            while (introductions.Count > 0 && target != null)
            {
                var boss = introductions.Dequeue();
                if (boss == null || !boss.IsAlive) continue;
                var start = transform.position;
                float fromZoom = cam.orthographicSize;
                float duration = Mathf.Max(0.01f, tuning.bossIntroPanSeconds);
                for (float elapsed = 0; elapsed < duration && boss != null && boss.IsAlive; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                    var bounds = VisibleBounds(boss.transform);
                    var destination = new Vector3(bounds.center.x, bounds.center.y, start.z);
                    transform.position = Vector3.Lerp(start, destination, t);
                    float zoom = Mathf.Max(savedZoom, bounds.extents.y * 1.2f, bounds.extents.x / Mathf.Max(0.1f, cam.aspect) * 1.2f);
                    cam.orthographicSize = Mathf.Lerp(fromZoom, zoom, t);
                    yield return null;
                }
                float hold = Mathf.Max(0, tuning.bossIntroHoldSeconds);
                for (float elapsed = 0; elapsed < hold && boss != null && boss.IsAlive && target != null; elapsed += Time.unscaledDeltaTime)
                {
                    var centre = VisibleBounds(boss.transform).center;
                    transform.position = new Vector3(centre.x, centre.y, transform.position.z);
                    yield return null;
                }
                start = transform.position;
                fromZoom = cam.orthographicSize;
                duration = Mathf.Max(0.01f, tuning.bossIntroReturnSeconds);
                for (float elapsed = 0; elapsed < duration && target != null; elapsed += Time.unscaledDeltaTime)
                {
                    float t = Mathf.SmoothStep(0, 1, elapsed / duration);
                    var destination = new Vector3(target.position.x, target.position.y, start.z);
                    transform.position = Vector3.Lerp(start, destination, t);
                    cam.orthographicSize = Mathf.Lerp(fromZoom, savedZoom, t);
                    yield return null;
                }
            }
        }
        finally { EndIntroduction(); }
    }

    void EndIntroduction()
    {
        if (!introActive) return;
        introActive = false;
        if (introOwner == this) introOwner = null;
        cam.orthographicSize = savedZoom;
        if (target != null) transform.position = new Vector3(target.position.x, target.position.y, transform.position.z);
        vel = Vector3.zero;
        introductions.Clear();
    }

    public static void CancelActiveIntroduction()
    {
        if (introOwner == null) return;
        var owner = introOwner;
        owner.StopAllCoroutines();
        owner.EndIntroduction();
    }

    void OnDisable()
    {
        StopAllCoroutines();
        EndIntroduction();
    }

    public static Bounds VisibleBounds(Transform subject)
    {
        var bounds = new Bounds(subject.position, Vector3.zero);
        bool found = false;
        foreach (var renderer in subject.GetComponentsInChildren<SpriteRenderer>())
        {
            if (!renderer.enabled || renderer.sprite == null) continue;
            if (!found) { bounds = renderer.bounds; found = true; }
            else bounds.Encapsulate(renderer.bounds);
        }
        return bounds;
    }

    public static bool FullyVisible(Camera camera, Transform subject, float margin)
    {
        if (camera == null || subject == null) return false;
        var bounds = VisibleBounds(subject);
        // All eight corners also work if the camera later gains a tilt.
        for (int i = 0; i < 8; i++)
        {
            var corner = bounds.center + Vector3.Scale(bounds.extents,
                new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
            var point = camera.WorldToViewportPoint(corner);
            if (point.z < camera.nearClipPlane || point.z > camera.farClipPlane || point.x < margin
                || point.x > 1 - margin || point.y < margin || point.y > 1 - margin) return false;
        }
        return true;
    }

    public static bool InCombatView(Camera camera, Transform subject, float margin)
    {
        if (camera == null || subject == null) return false;
        // Gameplay position, not padded art/shadows: huge bosses may never fit all
        // sprite corners on screen even while standing directly beside the player.
        var collider = subject.GetComponent<Collider2D>();
        var point = camera.WorldToViewportPoint(collider != null ? collider.bounds.center : subject.position);
        margin = Mathf.Clamp(margin, 0, 0.2f);
        return point.z >= camera.nearClipPlane && point.z <= camera.farClipPlane
            && point.x >= margin && point.x <= 1 - margin
            && point.y >= margin && point.y <= 1 - margin;
    }
}
