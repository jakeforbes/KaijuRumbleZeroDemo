using UnityEngine;

/// <summary>
/// Dead-zone follow camera. The player moves freely inside a central box; once they
/// push past it the camera pans to keep up. Zoom pulls back as the player grows, at
/// (scale ^ zoomExponent) — square root by default, so you visibly dominate more of
/// the frame without losing sight of what is coming at you.
/// </summary>
public class CameraRig : MonoBehaviour
{
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
        if (target == null) return;

        float scale = 1f;
        var player = target.GetComponent<PlayerController>();
        if (player != null) scale = player.Scale;

        // Zoom is relative to the kaiju's starting size, not its absolute scale.
        // Keyed to absolute scale, making the kaiju bigger also pulled the camera
        // back, which quietly cancelled most of the increase — so tierScale could
        // not be used to change how big the kaiju reads on screen.
        float startScale = tuning.tierScale != null && tuning.tierScale.Length > 0
            ? Mathf.Max(0.01f, tuning.tierScale[0]) : 1f;

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            tuning.baseOrthoSize * Mathf.Pow(scale / startScale, tuning.zoomExponent),
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
}
