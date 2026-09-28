using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the Revive popup art into Assets/SS/Popup/Revive, in the same military-metal style as the
// other popups (Assets/SS/Popup):
//  - revive_header / revive_button / revive_nothanks: the existing "mission paused", "resume" and "home"
//    art with their baked-in words painted out, so live Bulletproof text can go on top
//  - revive_gauge / revive_ring / revive_heart: new art drawn in the same palette
public static class ReviveArtGenerator
{
    public const string Folder = "Assets/SS/Popup/Revive";
    const string Source = "Assets/SS/Popup";

    [MenuItem("Tools/AirStrike/Generate Revive Popup Art")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder(Source, "Revive");

        // Boxes are (x, y, width, height) in image pixels measured from the TOP-left, around the baked words
        Blank("mission paused.png", "revive_header.png", Channel.Luminance,
              new RectInt(98, 18, 140, 46), new RectInt(86, 62, 162, 40));
        Blank("resume.png", "revive_button.png", Channel.Green, new RectInt(84, 48, 284, 90));
        Blank("home.png", "revive_nothanks.png", Channel.Red, new RectInt(88, 36, 270, 74));

        Save(Gauge(512), "revive_gauge.png");
        Save(Ring(512), "revive_ring.png");
        Save(Heart(256), "revive_heart.png");

        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles(Folder, "*.png")) MakeSprite(file.Replace('\\', '/'));
        Debug.Log("[ReviveArtGenerator] Revive art written to " + Folder);
    }

    // ---------------------------------------------------------------- painting out baked text

    enum Channel { Green, Red, Luminance }

    static void Blank(string sourceFile, string outFile, Channel channel, params RectInt[] boxesTopLeft)
    {
        var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        tex.LoadImage(File.ReadAllBytes(Path.Combine(Source, sourceFile)));
        int w = tex.width, h = tex.height;
        Color[] px = tex.GetPixels();

        foreach (RectInt topLeft in boxesTopLeft)
        {
            // to Unity's bottom-left pixel coordinates
            var box = new RectInt(topLeft.x, h - topLeft.y - topLeft.height, topLeft.width, topLeft.height);
            bool[] mask = new bool[w * h];

            for (int y = box.yMin; y < box.yMax; y++)
            {
                // Background reference for this row = pixels just outside the box on both sides
                Color bgL = px[y * w + Mathf.Max(0, box.xMin - 3)];
                Color bgR = px[y * w + Mathf.Min(w - 1, box.xMax + 2)];
                float bgKey = (Key(bgL, channel) + Key(bgR, channel)) * 0.5f;
                float bgLum = (Lum(bgL) + Lum(bgR)) * 0.5f;

                for (int x = box.xMin; x < box.xMax; x++)
                {
                    Color c = px[y * w + x];
                    bool isText = channel == Channel.Luminance
                        ? Mathf.Abs(Lum(c) - bgLum) > 0.10f
                        : Key(c, channel) < bgKey * 0.62f || Lum(c) > bgLum + 0.22f;
                    mask[y * w + x] = isText;
                }
            }

            Dilate(mask, w, h, box, 2);

            // Fill each masked run by blending between the clean pixels on its left and right
            for (int y = box.yMin; y < box.yMax; y++)
            {
                int x = box.xMin;
                while (x < box.xMax)
                {
                    if (!mask[y * w + x]) { x++; continue; }
                    int start = x;
                    while (x < box.xMax && mask[y * w + x]) x++;
                    Color left = px[y * w + Mathf.Max(0, start - 1)];
                    Color right = px[y * w + Mathf.Min(w - 1, x)];
                    for (int i = start; i < x; i++)
                    {
                        float t = (i - start + 1f) / (x - start + 1f);
                        Color fill = Color.Lerp(left, right, t);
                        fill.a = px[y * w + i].a;
                        px[y * w + i] = fill;
                    }
                }
            }
            // Soften the filled area vertically so the rows don't streak
            SmoothVertical(px, mask, w, h, box);

            // Glossy buttons: a wide horizontal median removes the faint ghost of the letters' drop
            // shadows while keeping the broad glow / gradient of the glass
            if (channel != Channel.Luminance)
            {
                MedianRows(px, w, box, 41);
                var all = new bool[w * h];
                for (int y = box.yMin; y < box.yMax; y++)
                    for (int x = box.xMin; x < box.xMax; x++) all[y * w + x] = true;
                SmoothVertical(px, all, w, h, box);
            }
        }

        tex.SetPixels(px);
        tex.Apply();
        Save(tex, outFile);
    }

    static float Key(Color c, Channel ch)
    {
        switch (ch)
        {
            case Channel.Green: return c.g - Mathf.Max(c.r, c.b);
            case Channel.Red: return c.r - Mathf.Max(c.g, c.b);
            default: return Lum(c);
        }
    }

    static float Lum(Color c) => 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;

    static void Dilate(bool[] mask, int w, int h, RectInt box, int radius)
    {
        var copy = (bool[])mask.Clone();
        for (int y = box.yMin; y < box.yMax; y++)
            for (int x = box.xMin; x < box.xMax; x++)
            {
                if (!copy[y * w + x]) continue;
                for (int dy = -radius; dy <= radius; dy++)
                    for (int dx = -radius; dx <= radius; dx++)
                    {
                        int nx = x + dx, ny = y + dy;
                        if (nx >= box.xMin && nx < box.xMax && ny >= box.yMin && ny < box.yMax) mask[ny * w + nx] = true;
                    }
            }
    }

    static void SmoothVertical(Color[] px, bool[] mask, int w, int h, RectInt box)
    {
        var src = (Color[])px.Clone();
        for (int y = box.yMin + 1; y < box.yMax - 1; y++)
            for (int x = box.xMin; x < box.xMax; x++)
            {
                if (!mask[y * w + x]) continue;
                Color c = (src[(y - 1) * w + x] + src[y * w + x] * 2f + src[(y + 1) * w + x]) / 4f;
                c.a = src[y * w + x].a;
                px[y * w + x] = c;
            }
    }

    static void MedianRows(Color[] px, int w, RectInt box, int window)
    {
        var src = (Color[])px.Clone();
        int half = window / 2;
        var rs = new List<float>(window); var gs = new List<float>(window); var bs = new List<float>(window);
        for (int y = box.yMin; y < box.yMax; y++)
            for (int x = box.xMin; x < box.xMax; x++)
            {
                rs.Clear(); gs.Clear(); bs.Clear();
                for (int i = Mathf.Max(box.xMin, x - half); i <= Mathf.Min(box.xMax - 1, x + half); i++)
                {
                    Color s = src[y * w + i];
                    rs.Add(s.r); gs.Add(s.g); bs.Add(s.b);
                }
                rs.Sort(); gs.Sort(); bs.Sort();
                int m = rs.Count / 2;
                px[y * w + x] = new Color(rs[m], gs[m], bs[m], src[y * w + x].a);
            }
    }

    // ---------------------------------------------------------------- new art

    // Round steel gauge: gritty bevelled bezel (lit from above) with 8 rivets, and a face made of the
    // same black diamond plate as the popup body (back.png)
    static Texture2D Gauge(int size)
    {
        var tex = NewTex(size);
        float c = size / 2f, R = size / 2f - 2f;
        var px = new Color[size * size];

        var plate = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        plate.LoadImage(File.ReadAllBytes(Path.Combine(Source, "back.png")));
        // One clean patch of diamond plate from inside back.png's frame (away from its corner screw),
        // stretched over the whole face so there are no repeat seams
        var plateArea = new RectInt(40, 40, 255, 255);
        float faceDiameter = R * 0.86f * 2f;

        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                float up = dy / R; // -1 bottom .. 1 top
                Color col = Color.clear;
                if (d <= R)
                {
                    if (d > R * 0.86f)
                    {
                        float grit = (Noise(x * 0.35f, y * 0.35f) - 0.5f) * 0.10f + (Noise(x * 0.05f, y * 0.9f) - 0.5f) * 0.06f;
                        float shade = 0.30f + 0.22f * up + grit;                               // dark steel, lit from above
                        float edge = Mathf.Abs(d - R * 0.93f) / (R * 0.07f);                    // rounded profile
                        shade += 0.10f * (1f - edge * edge);
                        if (d > R * 0.985f || (d < R * 0.875f && d > R * 0.86f)) shade *= 0.45f; // dark rims
                        col = new Color(shade * 0.96f, shade * 0.97f, shade * 0.92f, 1f);       // slightly warm, like the frames
                    }
                    else
                    {
                        float u = (x - (c - faceDiameter / 2f)) / faceDiameter;
                        float v = (y - (c - faceDiameter / 2f)) / faceDiameter;
                        col = plate.GetPixelBilinear((plateArea.x + u * plateArea.width) / plate.width,
                                                     (plateArea.y + v * plateArea.height) / plate.height);
                        float vignette = Mathf.Lerp(1.1f, 0.55f, Mathf.Pow(d / (R * 0.86f), 2f));
                        col = new Color(col.r * vignette, col.g * vignette, col.b * vignette, 1f);
                        if (d > R * 0.82f) col = Color.Lerp(col, Color.black, 0.55f);            // inner shadow under the bezel
                    }
                    col.a = Mathf.Clamp01(R - d + 0.5f);                                       // antialiased rim
                }
                px[y * size + x] = col;
            }
        Object.DestroyImmediate(plate);

        // rivets on the bezel
        for (int i = 0; i < 8; i++)
        {
            float a = i * Mathf.PI / 4f + Mathf.PI / 8f;
            Vector2 p = new Vector2(c + Mathf.Cos(a) * R * 0.93f, c + Mathf.Sin(a) * R * 0.93f);
            DrawRivet(px, size, p, R * 0.035f);
        }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static void DrawRivet(Color[] px, int size, Vector2 p, float r)
    {
        for (int y = (int)(p.y - r - 2); y <= p.y + r + 2; y++)
            for (int x = (int)(p.x - r - 2); x <= p.x + r + 2; x++)
            {
                if (x < 0 || y < 0 || x >= size || y >= size) continue;
                float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0f) continue;
                float shade = 0.22f + 0.40f * Mathf.Clamp01((dy - dx) / (2f * r) + 0.5f);
                px[y * size + x] = Color.Lerp(px[y * size + x], new Color(shade, shade, shade, 1f), a);
            }
    }

    // White ring (tinted in the UI) used twice: dark track + gold radial-filled countdown
    static Texture2D Ring(int size)
    {
        var tex = NewTex(size);
        float c = size / 2f, outer = size * 0.405f, inner = size * 0.345f;
        var px = new Color[size * size];
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float dx = x + 0.5f - c, dy = y + 0.5f - c, d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
                float mid = 1f - Mathf.Abs((d - (inner + outer) / 2f) / ((outer - inner) / 2f));
                float v = 0.78f + 0.22f * mid; // slightly brighter centre line = glossy tube
                px[y * size + x] = new Color(v, v, v, a);
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Glossy red heart with a dark outline
    static Texture2D Heart(int size)
    {
        var tex = NewTex(size);
        var px = new Color[size * size];
        const int ss = 3; // supersampling for smooth edges
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float fill = 0f, outline = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        float u = ((x + (sx + 0.5f) / ss) / size - 0.5f) * 2.6f;
                        float v = ((y + (sy + 0.5f) / ss) / size - 0.46f) * 2.6f;
                        float f = HeartField(u, v);
                        if (f <= 0f) outline += 1f;
                        if (HeartField(u * 1.12f, v * 1.12f + 0.03f) <= 0f) fill += 1f;
                    }
                fill /= ss * ss;
                outline /= ss * ss;
                if (outline <= 0f) { px[y * size + x] = Color.clear; continue; }

                float t = (float)y / size;
                Color body = Color.Lerp(new Color(0.62f, 0.04f, 0.07f), new Color(1f, 0.30f, 0.30f), t); // darker at the bottom
                Color col = Color.Lerp(new Color(0.16f, 0.03f, 0.03f), body, fill);

                // gloss highlight upper-left
                float gx = (x - size * 0.34f) / (size * 0.13f), gy = (y - size * 0.66f) / (size * 0.08f);
                float gloss = Mathf.Clamp01(1f - (gx * gx + gy * gy)) * fill;
                col = Color.Lerp(col, Color.white, gloss * 0.55f);
                col.a = outline;
                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Smooth value noise 0..1 (for metal grit)
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

    // Classic implicit heart curve: (x² + y² - 1)³ - x² y³ <= 0
    static float HeartField(float x, float y)
    {
        float a = x * x + y * y - 1f;
        return a * a * a - x * x * y * y * y;
    }

    // ---------------------------------------------------------------- io

    static Texture2D NewTex(int size) => new Texture2D(size, size, TextureFormat.RGBA32, false);

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
