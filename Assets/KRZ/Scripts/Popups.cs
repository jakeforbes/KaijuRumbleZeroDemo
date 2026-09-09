using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Floating damage numbers. Drawn in one IMGUI pass rather than one object each,
/// so a swarm landing hits at once costs nothing.
///
/// These exist to make damage tunable: without them, numbers on the Tuning asset
/// are guesses, because nothing on screen says how hard anything hit.
/// </summary>
public class Popups : MonoBehaviour
{
    struct Item
    {
        public Vector3 world;
        public string text;
        public Color colour;
        public float born;
        public float drift;
    }

    const float Life = 0.85f;

    static readonly List<Item> items = new();
    public static bool show = true;

    public static void Add(Vector3 world, string text, Color colour)
    {
        if (!show) return;
        items.Add(new Item
        {
            world = world,
            text = text,
            colour = colour,
            born = Time.time,
            drift = Random.Range(-24f, 24f)
        });
    }

    public static void Clear() => items.Clear();

    GUIStyle style;

    void OnGUI()
    {
        if (items.Count == 0) return;

        var cam = Camera.main;
        if (cam == null) return;

        style ??= new GUIStyle(GUI.skin.label)
        {
            fontSize = 15,
            alignment = TextAnchor.MiddleCenter,
            richText = true
        };

        for (int i = items.Count - 1; i >= 0; i--)
        {
            var it = items[i];
            float age = Time.time - it.born;
            if (age >= Life) { items.RemoveAt(i); continue; }

            float t = age / Life;
            Vector3 sp = cam.WorldToScreenPoint(it.world);
            if (sp.z < 0f) continue;

            float x = sp.x + it.drift * t;
            float y = Screen.height - sp.y - 30f - 46f * t;   // rises as it fades

            var c = it.colour;
            c.a = 1f - t * t;
            style.normal.textColor = c;
            GUI.Label(new Rect(x - 40f, y - 12f, 80f, 24f), it.text, style);
        }
    }
}
