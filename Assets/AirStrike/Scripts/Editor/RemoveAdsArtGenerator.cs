using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the Remove Ads button art into Assets/SS/Popup/RemoveAds, in the menu's military-metal style
// but in brushed gold so it stands apart from the PLAY / LEADERBOARD buttons:
//  - removeads_plate:  gold plate in a riveted dark-steel rim
//  - removeads_emblem: dark steel disc with a gold rim (holds the "AD" text)
//  - removeads_slash:  red "no" ring + bar drawn over the "AD"
//  - removeads_shine:  soft white band that sweeps across the plate
public static class RemoveAdsArtGenerator
{
    public const string Folder = "Assets/SS/Popup/RemoveAds";

    [MenuItem("Tools/AirStrike/Generate Remove Ads Art")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SS/Popup", "RemoveAds");

        Save(Plate(560, 168), "removeads_plate.png");
        Save(Emblem(256), "removeads_emblem.png");
        Save(Slash(256), "removeads_slash.png");
        Save(Shine(96, 256), "removeads_shine.png");
        // Offer panel
        Save(CloseButton(160), "removeads_close.png");
        Save(Check(128), "removeads_check.png");
        Save(Chevron(128, 160), "removeads_chevron.png");
        Save(ScreenFrame(600, 300), "removeads_screen.png");

        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles(Folder, "*.png")) MakeSprite(file.Replace('\\', '/'));
        Debug.Log("[RemoveAdsArtGenerator] Remove Ads art written to " + Folder);
    }

    // ---------------------------------------------------------------- plate

    static Texture2D Plate(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        float radius = h * 0.30f;
        const float rim = 11f, groove = 3f;

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float inside = -RoundedBox(x + 0.5f - w / 2f, y + 0.5f - h / 2f, w / 2f - 1f, h / 2f - 1f, radius); // px from edge, >0 inside
                if (inside <= -1f) { px[y * w + x] = Color.clear; continue; }
                float up = (y + 0.5f) / h; // 0 bottom .. 1 top
                Color col;

                if (inside < rim)
                {
                    // dark steel rim, rounded profile, lit from above
                    float grit = (Noise(x * 0.4f, y * 0.4f) - 0.5f) * 0.08f;
                    float profile = 1f - Mathf.Pow(Mathf.Abs(inside / rim - 0.45f) / 0.55f, 2f);
                    float s = 0.20f + 0.22f * up + 0.12f * profile + grit;
                    col = new Color(s * 0.97f, s * 0.97f, s * 0.93f, 1f);
                }
                else if (inside < rim + groove)
                {
                    col = new Color(0.06f, 0.05f, 0.03f, 1f); // dark groove between rim and gold
                }
                else
                {
                    // brushed gold: bright at the top, deep amber at the bottom, horizontal brush lines
                    Color top = new Color(1f, 0.88f, 0.48f), bottom = new Color(0.70f, 0.45f, 0.10f);
                    col = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, up));
                    float brush = (Noise(x * 0.015f, y * 0.9f) - 0.5f) * 0.10f + (Noise(x * 0.004f, y * 0.25f) - 0.5f) * 0.06f;
                    col = new Color(col.r + brush, col.g + brush * 0.9f, col.b + brush * 0.5f, 1f);
                    // glossy band across the upper third
                    float band = Mathf.Exp(-Mathf.Pow((up - 0.72f) / 0.07f, 2f));
                    col = Color.Lerp(col, new Color(1f, 0.97f, 0.82f), band * 0.35f);
                    // inner shadow under the rim
                    float shadow = Mathf.Clamp01(1f - (inside - rim - groove) / 9f);
                    col = Color.Lerp(col, new Color(0.25f, 0.14f, 0.02f), shadow * 0.55f);
                }
                col.a = Mathf.Clamp01(inside + 1f); // antialiased outline
                px[y * w + x] = col;
            }

        // rivets at both ends of the gold face
        foreach (float rx in new[] { 30f, w - 30f })
            DrawRivet(px, w, h, new Vector2(rx, h / 2f), 7.5f);

        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Signed distance to a rounded box centred on (0,0): negative inside
    static float RoundedBox(float px, float py, float halfW, float halfH, float r)
    {
        float qx = Mathf.Abs(px) - halfW + r, qy = Mathf.Abs(py) - halfH + r;
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    static void DrawRivet(Color[] px, int w, int h, Vector2 p, float r)
    {
        for (int y = (int)(p.y - r - 2); y <= p.y + r + 2; y++)
            for (int x = (int)(p.x - r - 2); x <= p.x + r + 2; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0f) continue;
                float s = 0.25f + 0.45f * Mathf.Clamp01((dy - dx) / (2f * r) + 0.5f);
                Color c = d > r - 1.5f ? new Color(0.08f, 0.07f, 0.05f, 1f) : new Color(s, s * 0.98f, s * 0.92f, 1f);
                px[y * w + x] = Color.Lerp(px[y * w + x], c, a);
            }
    }

    // ---------------------------------------------------------------- emblem / slash / shine

    // Dark steel disc with a gold rim
    static Texture2D Emblem(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float c = size / 2f, R = size * 0.47f, rimIn = size * 0.39f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > R + 1f) { px[y * size + x] = Color.clear; continue; }
                float up = dy / R;
                Color col;
                if (d > rimIn)
                {
                    float mid = 1f - Mathf.Abs((d - (R + rimIn) / 2f) / ((R - rimIn) / 2f));
                    col = Color.Lerp(new Color(0.62f, 0.38f, 0.07f), new Color(1f, 0.86f, 0.42f), 0.35f + 0.35f * up + 0.3f * mid);
                    if (d > R - 1.5f || d < rimIn + 1.5f) col *= 0.55f;
                }
                else
                {
                    float s = 0.10f + 0.10f * (1f - d / rimIn) + 0.05f * up;
                    col = new Color(s, s * 1.04f, s * 1.10f);
                }
                col.a = Mathf.Clamp01(R - d + 0.5f);
                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Red "no" sign: ring + diagonal bar, with a dark outline
    static Texture2D Slash(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float c = size / 2f, outer = size * 0.40f, inner = size * 0.33f, bar = size * 0.035f;
        Color red = new Color(0.92f, 0.12f, 0.08f), dark = new Color(0.25f, 0.02f, 0.02f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                float ring = Mathf.Min(outer - d, d - inner);                  // >0 inside the ring
                float diag = bar - Mathf.Abs(dx + dy) / 1.41421f;              // top-left to bottom-right bar
                if (d > inner) diag = -99f;
                float shape = Mathf.Max(ring, diag);
                float a = Mathf.Clamp01(shape + 2.5f);                          // outline 2.5px wide
                if (a <= 0f) { px[y * size + x] = Color.clear; continue; }
                Color col = Color.Lerp(dark, red, Mathf.Clamp01(shape + 0.5f));
                col = Color.Lerp(col, new Color(1f, 0.55f, 0.5f), Mathf.Clamp01(dy / outer) * 0.25f * Mathf.Clamp01(shape)); // lit top
                col.a = a;
                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Vertical soft band (rotated in the UI) for the shine sweep
    static Texture2D Shine(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float u = Mathf.Abs((x + 0.5f) / w * 2f - 1f);
                float a = Mathf.Pow(1f - u, 2.2f);
                px[y * w + x] = new Color(1f, 1f, 1f, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- offer panel art

    // Round red button in a steel rim with a white X
    static Texture2D CloseButton(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float c = size / 2f, R = size * 0.47f, rim = size * 0.39f, arm = size * 0.20f, thick = size * 0.055f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                if (d > R + 1f) { px[y * size + x] = Color.clear; continue; }
                float up = dy / R;
                Color col;
                if (d > rim)
                {
                    float s = 0.30f + 0.25f * up + (Noise(x * 0.4f, y * 0.4f) - 0.5f) * 0.08f;
                    col = new Color(s, s, s * 0.95f);
                    if (d > R - 1.5f || d < rim + 2f) col *= 0.5f;
                }
                else
                {
                    col = Color.Lerp(new Color(0.45f, 0.03f, 0.03f), new Color(0.95f, 0.20f, 0.15f), 0.5f + 0.5f * up);
                    float gloss = Mathf.Clamp01(1f - Mathf.Pow((dy - rim * 0.45f) / (rim * 0.35f), 2f) - Mathf.Pow(dx / (rim * 0.7f), 2f));
                    col = Color.Lerp(col, Color.white, gloss * 0.25f);
                    // white X (two rounded bars), dark edge
                    float a1 = Mathf.Abs(dx - dy) / 1.41421f, a2 = Mathf.Abs(dx + dy) / 1.41421f;
                    float along1 = Mathf.Abs(dx + dy) / 1.41421f, along2 = Mathf.Abs(dx - dy) / 1.41421f;
                    float bar = Mathf.Min(along1 <= arm ? a1 : 99f, along2 <= arm ? a2 : 99f);
                    if (bar < thick + 2f) col = Color.Lerp(col, new Color(0.25f, 0.02f, 0.02f), Mathf.Clamp01(thick + 2f - bar));
                    if (bar < thick) col = Color.Lerp(col, Color.white, Mathf.Clamp01(thick - bar + 0.5f));
                }
                col.a = Mathf.Clamp01(R - d + 0.5f);
                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Green disc with a white check mark
    static Texture2D Check(int size)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        float c = size / 2f, R = size * 0.46f, thick = size * 0.065f;
        Vector2 a = new Vector2(-0.24f, 0.02f) * size, b = new Vector2(-0.06f, -0.18f) * size, e = new Vector2(0.25f, 0.20f) * size;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x + 0.5f - c, y + 0.5f - c);
                float d = p.magnitude;
                if (d > R + 1f) { px[y * size + x] = Color.clear; continue; }
                float up = p.y / R;
                Color col = Color.Lerp(new Color(0.12f, 0.45f, 0.08f), new Color(0.45f, 0.85f, 0.25f), 0.5f + 0.5f * up);
                if (d > R - size * 0.06f) col = Color.Lerp(col, new Color(0.05f, 0.22f, 0.03f), 0.7f); // dark rim
                float dist = Mathf.Min(SegmentDistance(p, a, b), SegmentDistance(p, b, e));
                if (dist < thick + 2f) col = Color.Lerp(col, new Color(0.05f, 0.25f, 0.03f), Mathf.Clamp01(thick + 2f - dist) * 0.8f);
                if (dist < thick) col = Color.Lerp(col, Color.white, Mathf.Clamp01(thick - dist + 0.5f));
                col.a = Mathf.Clamp01(R - d + 0.5f);
                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static float SegmentDistance(Vector2 p, Vector2 a, Vector2 b)
    {
        Vector2 ab = b - a;
        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
        return (p - (a + ab * t)).magnitude;
    }

    // Gold chevron pointing left ("<"), dark outline
    static Texture2D Chevron(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        float thick = w * 0.16f;
        Vector2 tip = new Vector2(w * 0.22f, h * 0.5f), top = new Vector2(w * 0.78f, h * 0.88f), bottom = new Vector2(w * 0.78f, h * 0.12f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 p = new Vector2(x + 0.5f, y + 0.5f);
                float d = Mathf.Min(SegmentDistance(p, tip, top), SegmentDistance(p, tip, bottom));
                float outline = thick + 4f - d;
                if (outline <= 0f) { px[y * w + x] = Color.clear; continue; }
                float up = (float)y / h;
                Color gold = Color.Lerp(new Color(0.85f, 0.55f, 0.08f), new Color(1f, 0.93f, 0.55f), up);
                Color col = Color.Lerp(new Color(0.30f, 0.17f, 0.02f), gold, Mathf.Clamp01(thick - d + 0.5f));
                col.a = Mathf.Clamp01(outline);
                px[y * w + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Steel-framed "screen" with dark teal glass and scan lines (the picture area of the offer)
    static Texture2D ScreenFrame(int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        var px = new Color[w * h];
        float radius = 28f;
        const float frame = 16f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float inside = -RoundedBox(x + 0.5f - w / 2f, y + 0.5f - h / 2f, w / 2f - 1f, h / 2f - 1f, radius);
                if (inside <= -1f) { px[y * w + x] = Color.clear; continue; }
                float up = (y + 0.5f) / h;
                Color col;
                if (inside < frame)
                {
                    float profile = 1f - Mathf.Pow(Mathf.Abs(inside / frame - 0.45f) / 0.55f, 2f);
                    float s = 0.22f + 0.22f * up + 0.12f * profile + (Noise(x * 0.4f, y * 0.4f) - 0.5f) * 0.08f;
                    col = new Color(s * 0.97f, s * 0.97f, s * 0.93f);
                }
                else
                {
                    float u = (x + 0.5f) / w - 0.5f, v = up - 0.5f;
                    float vignette = 1f - (u * u * 1.6f + v * v * 2.2f);
                    col = Color.Lerp(new Color(0.02f, 0.08f, 0.10f), new Color(0.06f, 0.30f, 0.34f), Mathf.Clamp01(vignette));
                    if (y % 4 == 0) col *= 0.82f;                                             // scan lines
                    float shadow = Mathf.Clamp01(1f - (inside - frame) / 10f);               // inner shadow
                    col = Color.Lerp(col, Color.black, shadow * 0.6f);
                    float glare = Mathf.Clamp01(1f - Mathf.Abs((u + v * 0.8f) + 0.25f) / 0.08f) * 0.10f; // glass glare
                    col = Color.Lerp(col, Color.white, glare);
                }
                col.a = Mathf.Clamp01(inside + 1f);
                px[y * w + x] = col;
            }
        foreach (Vector2 p in new[] { new Vector2(9f, 9f), new Vector2(w - 9f, 9f), new Vector2(9f, h - 9f), new Vector2(w - 9f, h - 9f) })
            DrawRivet(px, w, h, p, 5f);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // ---------------------------------------------------------------- helpers

    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
        float a = Hash(xi, yi), b = Hash(xi + 1, yi), c = Hash(xi, yi + 1), d = Hash(xi + 1, yi + 1);
        return Mathf.Lerp(Mathf.Lerp(a, b, u), Mathf.Lerp(c, d, u), v);
    }

    static float Hash(int x, int y)
    {
        unchecked
        {
            uint h = (uint)(x * 374761393 + y * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    static void Save(Texture2D tex, string file)
    {
        File.WriteAllBytes(Path.Combine(Folder, file), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void MakeSprite(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.SaveAndReimport();
    }
}
