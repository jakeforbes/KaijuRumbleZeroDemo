using UnityEngine;

/// <summary>
/// Arcs of electricity crawling over a body. Purely cosmetic, and built out of the
/// same short-lived line segments the Blast tracer uses rather than a particle system,
/// so it costs nothing to set up and inherits the project's no-Editor-wiring rule.
///
/// An arc is three jagged segments between two points on the silhouette. Randomising
/// the count, the timing and the path every tick is what makes it read as electricity
/// instead of a looping decoration — a regular pulse would read as a machine.
/// </summary>
public class ElectricFx : MonoBehaviour
{
    /// <summary>Drawn height of the body, in world units. Arcs stay inside it.</summary>
    public float height = 6f;

    /// <summary>Half-width of the body, in world units.</summary>
    public float halfWidth = 2f;

    public float interval = 0.11f;
    public float ppu = 128f;
    public Color colour = new(0.75f, 1f, 1f);

    float nextAt;

    public static ElectricFx Attach(GameObject target, float height, float halfWidth, float ppu, Color colour)
    {
        var fx = target.AddComponent<ElectricFx>();
        fx.height = height;
        fx.halfWidth = halfWidth;
        fx.ppu = ppu;
        fx.colour = colour;
        return fx;
    }

    void Update()
    {
        if (Time.time < nextAt) return;

        // Jittered rather than fixed, so the rhythm never becomes predictable.
        nextAt = Time.time + interval * Random.Range(0.45f, 1.6f);

        int arcs = Random.value < 0.25f ? 2 : 1;
        for (int i = 0; i < arcs; i++) Arc();
    }

    void Arc()
    {
        Vector3 a = PointOnBody();
        Vector3 b = PointOnBody();

        // Too short reads as a speck, too long reads as a wire draped over the model.
        float span = Vector3.Distance(a, b);
        if (span < height * 0.15f || span > height * 0.75f) return;

        const int Segments = 3;
        float jitter = height * 0.09f;
        Vector3 previous = a;

        for (int s = 1; s <= Segments; s++)
        {
            Vector3 next = Vector3.Lerp(a, b, s / (float)Segments);
            if (s < Segments) next += (Vector3)(Random.insideUnitCircle * jitter);

            HitFx.Line(previous, next, colour, ppu, 0.07f, height * 0.018f);
            previous = next;
        }
    }

    /// <summary>
    /// Somewhere on the body, biased upward. The pivot sits at the feet, and arcs
    /// pooling around the ankles look like a puddle rather than a charge.
    /// </summary>
    Vector3 PointOnBody()
    {
        float up = Mathf.Lerp(0.15f, 1f, Mathf.Sqrt(Random.value)) * height;
        float across = Random.Range(-1f, 1f) * halfWidth;

        // Narrower toward the top, so arcs follow the silhouette rather than a box.
        across *= Mathf.Lerp(1f, 0.55f, up / height);

        return transform.position + new Vector3(across, up, 0f);
    }
}
