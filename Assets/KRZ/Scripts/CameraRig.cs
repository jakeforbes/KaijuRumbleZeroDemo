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

        cam.orthographicSize = Mathf.Lerp(
            cam.orthographicSize,
            tuning.baseOrthoSize * Mathf.Pow(scale, tuning.zoomExponent),
            1f - Mathf.Exp(-6f * Time.deltaTime));

        float halfH = cam.orthographicSize;
        float halfW = halfH * cam.aspect;

        Vector3 pos = transform.position;
        Vector2 offset = (Vector2)target.position - (Vector2)pos;

        float boxW = halfW * (1f - tuning.deadZoneX * 2f);
        float boxH = halfH * (1f - tuning.deadZoneY * 2f);

        Vector3 want = pos;
        if (Mathf.Abs(offset.x) > boxW) want.x += offset.x - Mathf.Sign(offset.x) * boxW;
        if (Mathf.Abs(offset.y) > boxH) want.y += offset.y - Mathf.Sign(offset.y) * boxH;

        transform.position = Vector3.SmoothDamp(pos, want, ref vel, tuning.followLag);
    }
}
