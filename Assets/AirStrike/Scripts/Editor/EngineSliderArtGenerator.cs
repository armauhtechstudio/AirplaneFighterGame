using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the engine (throttle) slider art into Assets/SS/Gameplay/Engine, in the same military-metal
// style as the popups: steel frame + black diamond plate (from Assets/SS/Popup/back.png), gold bevels.
//  - engine_track.png  : steel-framed slot with diamond-plate inside, rivets at both ends (9-sliced)
//  - engine_fill.png   : glossy white capsule, tinted green -> gold -> red by power (9-sliced)
//  - engine_ticks.png  : one scale tick, tiled up the side of the track
//  - engine_knob.png   : steel throttle grip with a gold bevel, for the "ENGINE" handle
public static class EngineSliderArtGenerator
{
    public const string Folder = "Assets/SS/Gameplay/Engine";

    [MenuItem("Tools/AirStrike/Generate Engine Slider Art")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SS/Gameplay", "Engine");

        Save(Track(128, 512), "engine_track.png");
        Save(Fill(64, 256), "engine_fill.png");
        Save(Ticks(48, 40), "engine_ticks.png");
        Save(Knob(256, 200), "engine_knob.png");
        Save(Shine(32, 96), "engine_shine.png");
        Save(Glow(128), "engine_glow.png");
        // Horizontal versions for the health bar (same art turned sideways: rivets at the ends)
        Save(Rotate(Track(128, 512)), "health_track.png");
        Save(Rotate(Fill(64, 256)), "health_fill.png");
        Save(Rotate(Shine(32, 96)), "health_shine.png");
        AssetDatabase.Refresh();
        MakeSprite("engine_shine.png", Vector4.zero);
        MakeSprite("engine_glow.png", Vector4.zero);
        MakeSprite("health_track.png", new Vector4(56, 40, 56, 40));
        MakeSprite("health_fill.png", new Vector4(24, 20, 24, 20));
        MakeSprite("health_shine.png", Vector4.zero);

        MakeSprite("engine_track.png", new Vector4(40, 56, 40, 56));
        MakeSprite("engine_fill.png", new Vector4(20, 24, 20, 24));
        MakeSprite("engine_ticks.png", Vector4.zero, TextureWrapMode.Repeat);
        MakeSprite("engine_knob.png", Vector4.zero);
        Debug.Log("[EngineSliderArtGenerator] Engine slider art written to " + Folder);
    }

    // ---------------------------------------------------------------- track

    static Texture2D Track(int w, int h)
    {
        var plate = LoadPlate();
        var px = new Color[w * h];
        const float frame = 18f, radius = 26f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, w, h, radius); // <0 inside
                if (d > 0.5f) { px[y * w + x] = Color.clear; continue; }
                float a = Mathf.Clamp01(0.5f - d);
                Color col;
                if (d > -frame)
                {
                    // steel frame: lit from the top-left, gritty
                    float lit = 0.34f + 0.16f * ((float)y / h - 0.5f) - 0.10f * ((float)x / w - 0.5f);
                    float bevel = 1f - Mathf.Abs(d + frame * 0.5f) / (frame * 0.5f);
                    lit += 0.10f * bevel + (Noise(x * 0.4f, y * 0.4f) - 0.5f) * 0.10f + (Noise(x * 0.06f, y * 0.5f) - 0.5f) * 0.05f;
                    if (d > -2.5f || (d < -frame + 3f)) lit *= 0.45f; // dark outer edge + inner lip
                    col = new Color(lit * 0.97f, lit * 0.98f, lit * 0.93f, a);
                }
                else
                {
                    // slot: black diamond plate with a soft inner shadow
                    Color p = plate.GetPixelBilinear(0.12f + 0.5f * x / w, 0.12f + 0.75f * y / h);
                    float shade = Mathf.Lerp(0.45f, 1f, Mathf.Clamp01((-d - frame) / 14f));
                    col = new Color(p.r * shade, p.g * shade, p.b * shade, a);
                }
                px[y * w + x] = col;
            }

        // rivets at both ends of the frame
        foreach (float yy in new[] { frame * 0.5f + 2f, h - frame * 0.5f - 2f })
            foreach (float xx in new[] { w * 0.3f, w * 0.7f })
                Rivet(px, w, h, new Vector2(xx, yy), 5.5f);

        Object.DestroyImmediate(plate);
        return ToTex(px, w, h);
    }

    // ---------------------------------------------------------------- fill

    static Texture2D Fill(int w, int h)
    {
        var px = new Color[w * h];
        float radius = w * 0.5f - 2f;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = RoundedRectDistance(x + 0.5f, y + 0.5f, w, h, radius);
                if (d > 0.5f) { px[y * w + x] = Color.clear; continue; }
                float a = Mathf.Clamp01(0.5f - d);
                // glossy tube: bright centre line, darker edges, highlight stripe on the left
                float across = Mathf.Abs((x + 0.5f) / w - 0.5f) * 2f;
                float v = Mathf.Lerp(1f, 0.55f, across * across);
                float stripe = Mathf.Clamp01(1f - Mathf.Abs((x + 0.5f) / w - 0.32f) / 0.08f);
                v = Mathf.Min(1f, v + 0.35f * stripe);
                px[y * w + x] = new Color(v, v, v, a);
            }
        return ToTex(px, w, h);
    }

    // ---------------------------------------------------------------- ticks

    static Texture2D Ticks(int w, int h)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                bool line = y >= h / 2 - 2 && y <= h / 2 + 1 && x >= w * 0.25f;
                px[y * w + x] = line ? new Color(0.85f, 0.86f, 0.80f, y == h / 2 - 2 ? 0.45f : 0.9f) : Color.clear;
            }
        return ToTex(px, w, h);
    }

    // ---------------------------------------------------------------- knob

    static Texture2D Knob(int w, int h)
    {
        var plate = LoadPlate();
        var px = new Color[w * h];
        float radius = 34f;
        Color goldLight = new Color(1f, 0.86f, 0.35f), goldDark = new Color(0.55f, 0.36f, 0.05f);
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float d = ChamferedDistance(x + 0.5f, y + 0.5f, w, h, radius);
                if (d > 0.5f) { px[y * w + x] = Color.clear; continue; }
                float a = Mathf.Clamp01(0.5f - d);
                float t = (float)y / h;
                Color col;
                if (d > -6f) col = new Color(0.08f, 0.07f, 0.05f);                           // dark outline
                else if (d > -20f)
                {
                    float bevel = 1f - Mathf.Abs(d + 13f) / 7f;                               // gold bevel ring
                    col = Color.Lerp(goldDark, goldLight, Mathf.Clamp01(t * 0.9f + 0.25f * bevel));
                    col *= 0.85f + 0.2f * Noise(x * 0.5f, y * 0.5f);
                }
                else if (d > -26f) col = new Color(0.05f, 0.05f, 0.05f);                     // inner lip
                else
                {
                    Color p = plate.GetPixelBilinear(0.1f + 0.6f * x / w, 0.1f + 0.5f * y / h);
                    float shade = Mathf.Lerp(0.8f, 1.35f, t);                                 // steel face, lit from above
                    col = new Color(p.r * shade + 0.05f, p.g * shade + 0.05f, p.b * shade + 0.05f);
                }
                col.a = a;
                px[y * w + x] = col;
            }
        Object.DestroyImmediate(plate);
        return ToTex(px, w, h);
    }

    // ---------------------------------------------------------------- motion helpers

    // Soft horizontal light band (fades out at top and bottom) that flows up through the fill
    static Texture2D Shine(int w, int h)
    {
        var px = new Color[w * h];
        for (int y = 0; y < h; y++)
        {
            float t = (y + 0.5f) / h;
            float a = Mathf.Pow(Mathf.Sin(t * Mathf.PI), 2f) * 0.8f;
            for (int x = 0; x < w; x++) px[y * w + x] = new Color(1f, 1f, 1f, a);
        }
        return ToTex(px, w, h);
    }

    // Soft round glow (tinted at runtime) behind the knob
    static Texture2D Glow(int size)
    {
        var px = new Color[size * size];
        float c = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - c, y + 0.5f - c).magnitude / c;
                float a = Mathf.Clamp01(1f - d);
                px[y * size + x] = new Color(1f, 1f, 1f, a * a);
            }
        return ToTex(px, size, size);
    }

    // ---------------------------------------------------------------- helpers

    static Texture2D LoadPlate()
    {
        var plate = new Texture2D(2, 2, TextureFormat.RGBA32, false);
        plate.LoadImage(File.ReadAllBytes("Assets/SS/Popup/back.png"));
        return plate;
    }

    // Signed distance to a rounded rectangle filling (0,0)-(w,h): negative inside
    static float RoundedRectDistance(float x, float y, float w, float h, float r)
    {
        float qx = Mathf.Abs(x - w / 2f) - (w / 2f - r);
        float qy = Mathf.Abs(y - h / 2f) - (h / 2f - r);
        float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude;
        return outside + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
    }

    // Rectangle with cut (45°) corners, like the popup buttons
    static float ChamferedDistance(float x, float y, float w, float h, float c)
    {
        float dx = Mathf.Abs(x - w / 2f) - w / 2f;
        float dy = Mathf.Abs(y - h / 2f) - h / 2f;
        float box = Mathf.Max(dx, dy);
        float corner = (dx + dy + c) / 1.4142f;
        return Mathf.Max(box, corner);
    }

    static void Rivet(Color[] px, int w, int h, Vector2 p, float r)
    {
        for (int y = (int)(p.y - r - 2); y <= p.y + r + 2; y++)
            for (int x = (int)(p.x - r - 2); x <= p.x + r + 2; x++)
            {
                if (x < 0 || y < 0 || x >= w || y >= h) continue;
                float dx = x + 0.5f - p.x, dy = y + 0.5f - p.y, d = Mathf.Sqrt(dx * dx + dy * dy);
                float a = Mathf.Clamp01(r - d + 0.5f);
                if (a <= 0f) continue;
                float shade = 0.22f + 0.40f * Mathf.Clamp01((dy - dx) / (2f * r) + 0.5f);
                Color c = px[y * w + x];
                px[y * w + x] = Color.Lerp(c, new Color(shade, shade, shade, c.a), a);
            }
    }

    static float Noise(float x, float y)
    {
        int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
        float xf = x - xi, yf = y - yi;
        float u = xf * xf * (3f - 2f * xf), v = yf * yf * (3f - 2f * yf);
        return Mathf.Lerp(Mathf.Lerp(Hash(xi, yi), Hash(xi + 1, yi), u), Mathf.Lerp(Hash(xi, yi + 1), Hash(xi + 1, yi + 1), u), v);
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

    // 90° turn: a tall texture becomes a wide one
    static Texture2D Rotate(Texture2D src)
    {
        int w = src.width, h = src.height;
        Color[] s = src.GetPixels();
        var d = new Color[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                d[x * h + (h - 1 - y)] = s[y * w + x]; // new size h x w
        Object.DestroyImmediate(src);
        return ToTex(d, h, w);
    }

    static Texture2D ToTex(Color[] px, int w, int h)
    {
        var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static void Save(Texture2D tex, string file)
    {
        File.WriteAllBytes(Path.Combine(Folder, file), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void MakeSprite(string file, Vector4 border, TextureWrapMode wrap = TextureWrapMode.Clamp)
    {
        var importer = AssetImporter.GetAtPath($"{Folder}/{file}") as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = wrap;
        importer.spriteBorder = border; // left, bottom, right, top (9-slice)
        importer.SaveAndReimport();
    }
}
