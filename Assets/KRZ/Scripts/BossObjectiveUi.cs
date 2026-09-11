using UnityEngine;

/// <summary>Always-on boss guidance and an unscaled victory celebration.</summary>
public sealed class BossObjectiveUi : MonoBehaviour
{
    Enemy boss;
    Camera view;
    Texture2D arrow;
    float nextBurst;
    float arrowAlpha;
    Vector2 lastDirection = Vector2.up;

    void Update()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null) return;
        if (view == null) view = Camera.main;
        if (progress.HasWon)
        {
            if (view != null && Time.unscaledTime >= nextBurst)
            {
                nextBurst = Time.unscaledTime + Random.Range(.22f, .45f);
                var position = view.ViewportToWorldPoint(new Vector3(Random.Range(.12f, .88f), Random.Range(.18f, .9f),
                    Mathf.Abs(view.transform.position.z - progress.transform.position.z)));
                position.z = progress.transform.position.z;
                float radius = view.orthographicSize * Random.Range(.12f, .26f);
                var colour = Color.HSVToRGB(Random.value, .72f, 1f);
                ShockwaveFx.Show(position, colour, radius, 1f, 1.1f, unscaled: true);
                ShockwaveFx.Show(position, Color.Lerp(colour, Color.white, .6f), radius * .65f, 1f, .75f, unscaled: true);
            }
            return;
        }
        if (boss == null || !boss.IsAlive)
        {
            boss = null;
            foreach (var enemy in Enemy.All)
                if (enemy != null && enemy.IsAlive && enemy.IsBoss) { boss = enemy; break; }
        }
    }

    void LateUpdate()
    {
        bool offScreen = false;
        if (view != null && boss != null && boss.IsAlive)
        {
            var point = view.WorldToViewportPoint(boss.transform.position);
            offScreen = point.z <= 0f || point.x < 0f || point.x > 1f || point.y < 0f || point.y > 1f;
        }
        arrowAlpha = Mathf.MoveTowards(arrowAlpha, offScreen ? 1f : 0f, Time.unscaledDeltaTime / .25f);
    }

    void OnGUI()
    {
        var progress = PlayerProgress.Instance;
        if (progress == null || progress.RunOver || view == null || boss == null || !boss.IsAlive) return;
        if (arrow == null) BuildArrow();
        var safe = Screen.safeArea;
        var bounds = new Rect(safe.x + 30f, Screen.height - safe.yMax + 30f,
            Mathf.Max(1f, safe.width - 60f), Mathf.Max(1f, safe.height - 90f));
        Vector3 screen = view.WorldToScreenPoint(boss.transform.position);
        Vector2 direction = new Vector2(screen.x, Screen.height - screen.y) - bounds.center;
        if (screen.z < 0f) direction = -direction;
        if (direction.sqrMagnitude > .01f) lastDirection = direction.normalized;
        direction = lastDirection;
        float extent = Mathf.Min(bounds.width * .5f / Mathf.Max(.0001f, Mathf.Abs(direction.x)),
            bounds.height * .5f / Mathf.Max(.0001f, Mathf.Abs(direction.y)));
        Vector2 anchor = bounds.center + direction * extent;
        var matrix = GUI.matrix;
        var colour = GUI.color;
        GUIUtility.RotateAroundPivot(Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg, anchor);
        float size = 34f + 3f * Mathf.Sin(Time.unscaledTime * 5f);
        GUI.color = new Color(1f, 1f, 1f, arrowAlpha);
        GUI.DrawTexture(new Rect(anchor.x - size * .5f, anchor.y - size * .5f, size, size), arrow);
        GUI.matrix = matrix;
        GUI.color = colour;
    }

    void BuildArrow()
    {
        arrow = new Texture2D(64, 64, TextureFormat.RGBA32, false) { name = "Boss direction triangle", filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[64 * 64];
        for (int y = 0; y < 64; y++)
        for (int x = 0; x < 64; x++)
        {
            float edge = Mathf.Min(x - 9f, (56f - x) * .57f - Mathf.Abs(y - 31.5f));
            pixels[y * 64 + x] = edge < 0 ? Color.clear : edge < 3f
                ? new Color(.015f, .15f, .045f, 1f) : new Color(.25f, 1f, .35f, 1f);
        }
        arrow.SetPixels(pixels);
        arrow.Apply(false, true);
    }

    void OnDestroy() { if (arrow != null) Destroy(arrow); }
}
