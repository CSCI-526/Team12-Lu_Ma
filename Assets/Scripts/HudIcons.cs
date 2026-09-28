// HudIcons.cs
// ---------------------------------------------------------------------------
// Small icons for the HUD, painted in code (the project has no image files):
//   - Skull()  : the FRENZY weapon
//   - Rocket() : the RPG weapon (a rocket-propelled grenade, pointing up-right)
//
// Each icon is a white shape on a transparent background, so an Image tints it
// with its colour. A shape is described by a test "is this point inside?" on a
// square from (0,0) bottom-left to (1,1) top-right; every pixel is sampled
// Samples x Samples times so the edges come out smooth.
// Built once, the first time they are asked for.
// ---------------------------------------------------------------------------
using UnityEngine;

public static class HudIcons
{
    private const int Size = 128;    // pixels per side
    private const int Samples = 4;   // per pixel and per axis (4 x 4 = 16 tests)

    private static Sprite skull;
    private static Sprite rocket;

    public static Sprite Skull()
    {
        if (skull == null)
        {
            skull = Paint("SkullIcon", InSkull);
        }
        return skull;
    }

    public static Sprite Rocket()
    {
        if (rocket == null)
        {
            rocket = Paint("RocketIcon", InRocket);
        }
        return rocket;
    }

    // ---- Shapes ----

    // A cranium (a wide ellipse) and a jaw under it, minus two eye holes, a nose
    // hole and the gaps between the teeth.
    private static bool InSkull(float x, float y)
    {
        bool head = InEllipse(x, y, 0.5f, 0.58f, 0.40f, 0.36f)
            || InRect(x, y, 0.29f, 0.11f, 0.71f, 0.40f);
        if (!head)
        {
            return false;
        }
        if (InEllipse(x, y, 0.34f, 0.55f, 0.11f, 0.12f) || InEllipse(x, y, 0.66f, 0.55f, 0.11f, 0.12f))
        {
            return false; // eyes
        }
        if (InTriangle(x, y, new Vector2(0.5f, 0.45f), new Vector2(0.44f, 0.34f), new Vector2(0.56f, 0.34f)))
        {
            return false; // nose
        }
        if (y < 0.26f && (Near(x, 0.39f, 0.014f) || Near(x, 0.5f, 0.014f) || Near(x, 0.61f, 0.014f)))
        {
            return false; // gaps between the teeth
        }
        return !(y < 0.285f && y > 0.26f && x > 0.33f && x < 0.67f); // where the teeth meet the jaw
    }

    // An RPG: a fat pointed warhead, a thin tube and fins at the back, drawn
    // along a line from the bottom-left to the top-right corner.
    private static bool InRocket(float x, float y)
    {
        // u = along the rocket (toward the tip), v = across it.
        const float cos = 0.70710678f; // 45 degrees
        float dx = x - 0.5f;
        float dy = y - 0.5f;
        float u = (dx + dy) * cos;
        float v = Mathf.Abs(-dx + dy) * cos;

        // Warhead: swells from the tube to its widest point (30% along), then
        // closes to a sharp nose. A thin band is cut across it at the widest point.
        if (u >= 0.06f && u <= 0.47f)
        {
            float t = (u - 0.06f) / 0.41f;
            if (t > 0.30f && t < 0.345f)
            {
                return false; // the band
            }
            float half = t < 0.3f
                ? Mathf.Lerp(0.055f, 0.16f, Mathf.Sin(t / 0.3f * Mathf.PI * 0.5f))
                : 0.16f * (1f - (t - 0.3f) / 0.7f);
            return v <= half;
        }
        // Tube, from the tail to the warhead.
        if (u >= -0.46f && u < 0.06f)
        {
            if (v <= 0.042f)
            {
                return true;
            }
            // Two fins near the back, swept back, with a square rear edge.
            if (u >= -0.40f && u < -0.24f)
            {
                float s = (-0.24f - u) / 0.16f;
                return v <= 0.042f + 0.10f * s;
            }
        }
        return false;
    }

    // ---- Helpers ----

    private static bool InEllipse(float x, float y, float cx, float cy, float rx, float ry)
    {
        float a = (x - cx) / rx;
        float b = (y - cy) / ry;
        return a * a + b * b <= 1f;
    }

    private static bool InRect(float x, float y, float xMin, float yMin, float xMax, float yMax)
    {
        return x >= xMin && x <= xMax && y >= yMin && y <= yMax;
    }

    private static bool Near(float value, float centre, float halfWidth)
    {
        return Mathf.Abs(value - centre) <= halfWidth;
    }

    private static bool InTriangle(float x, float y, Vector2 a, Vector2 b, Vector2 c)
    {
        float d1 = Side(x, y, a, b);
        float d2 = Side(x, y, b, c);
        float d3 = Side(x, y, c, a);
        bool hasNegative = d1 < 0f || d2 < 0f || d3 < 0f;
        bool hasPositive = d1 > 0f || d2 > 0f || d3 > 0f;
        return !(hasNegative && hasPositive);
    }

    private static float Side(float x, float y, Vector2 a, Vector2 b)
    {
        return (x - b.x) * (a.y - b.y) - (a.x - b.x) * (y - b.y);
    }

    // Paints the shape into a white-on-transparent texture and wraps it in a Sprite.
    private static Sprite Paint(string iconName, System.Func<float, float, bool> inside)
    {
        Texture2D texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        texture.name = iconName;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Color32[] pixels = new Color32[Size * Size];
        float step = 1f / (Size * Samples);
        for (int py = 0; py < Size; py++)
        {
            for (int px = 0; px < Size; px++)
            {
                int hits = 0;
                for (int sy = 0; sy < Samples; sy++)
                {
                    for (int sx = 0; sx < Samples; sx++)
                    {
                        float x = (px * Samples + sx + 0.5f) * step;
                        float y = (py * Samples + sy + 0.5f) * step;
                        if (inside(x, y))
                        {
                            hits += 1;
                        }
                    }
                }
                byte alpha = (byte)(255 * hits / (Samples * Samples));
                pixels[py * Size + px] = new Color32(255, 255, 255, alpha);
            }
        }
        texture.SetPixels32(pixels);
        texture.Apply();

        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, Size, Size), new Vector2(0.5f, 0.5f), 100f);
        sprite.name = iconName;
        return sprite;
    }
}
