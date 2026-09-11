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

    /// <summary>
    /// A one-shot fan of arcs spidering out across a ground cone, for the Slam.
    ///
    /// Unlike the crawling arcs this class normally draws, nothing owns this: it fires
    /// once over an area, and the area is the whole point. Bolt count, reach and
    /// thickness all come off the range it is handed, so the burst is how the player
    /// reads how far the blow actually landed — an effect that stayed the same size
    /// while the damage grew would teach the wrong range.
    ///
    /// Drawn on the squashed ground plane, so the fan lies flat in the street instead
    /// of standing up like a wall.
    /// </summary>
    public static void Burst(Vector3 origin, Vector2 aimFlat, float range, float arcDegrees,
                             float isoSquash, float ppu, Color colour, int bolts)
    {
        if (range <= 0f || bolts <= 0) return;

        float half = arcDegrees * 0.5f * Mathf.Deg2Rad;
        float baseAngle = Mathf.Atan2(aimFlat.y, aimFlat.x);

        for (int i = 0; i < bolts; i++)
        {
            float angle = baseAngle + Random.Range(-half, half);

            // Never the full reach, so the fan has a ragged edge rather than reading
            // as a drawn circle with the radius written on it.
            float reach = range * Random.Range(0.45f, 1f);

            var flat = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
            Vector3 tip = origin + (Vector3)(new Vector2(flat.x, flat.y * isoSquash) * reach);

            const int Segments = 3;
            float jitter = reach * 0.16f;
            Vector3 previous = origin;

            for (int s = 1; s <= Segments; s++)
            {
                Vector3 next = Vector3.Lerp(origin, tip, s / (float)Segments);
                if (s < Segments) next += (Vector3)(Random.insideUnitCircle * jitter);

                HitFx.Line(previous, next, colour, ppu, 0.16f, reach * 0.035f);
                previous = next;
            }
        }
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
