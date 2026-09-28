using System.IO;
using UnityEditor;
using UnityEngine;

// Rectangular leaderboard bars (replacing the rounded Assets/SS/obj.png lozenge), in the popups'
// teal-glass / military style: cut (45°) corners, dark outline, bevelled frame lit from the top,
// deep gradient face with a soft top glow and a gloss line. 9-sliced so they stretch to any width.
//   leaderboard_row.png      - teal (normal rows, name box, menu button)
//   leaderboard_row_gold.png - gold (the player's own row, "Your Rank")
public static class LeaderboardArtGenerator
{
    public const string Folder = "Assets/SS/Popup/Leaderboard";
    public const string Teal = Folder + "/leaderboard_row.png";
    public const string GoldPath = Folder + "/leaderboard_row_gold.png";
    const int W = 512, H = 128, Border = 28;

    struct Palette
    {
        public Color outline, frameLight, frameDark, faceTop, faceBottom, glow, accent;
    }

    static readonly Palette TealPalette = new Palette
    {
        outline = new Color(0.03f, 0.06f, 0.07f),
        frameLight = new Color(0.36f, 0.86f, 0.78f),
        frameDark = new Color(0.07f, 0.30f, 0.30f),
        faceTop = new Color(0.10f, 0.30f, 0.30f),
        faceBottom = new Color(0.03f, 0.10f, 0.11f),
        glow = new Color(0.30f, 0.80f, 0.72f),
        accent = new Color(0.45f, 0.95f, 0.85f),
    };

    static readonly Palette GoldPalette = new Palette
    {
        outline = new Color(0.08f, 0.05f, 0.01f),
        frameLight = new Color(1.00f, 0.86f, 0.40f),
        frameDark = new Color(0.45f, 0.28f, 0.04f),
        faceTop = new Color(0.36f, 0.25f, 0.06f),
        faceBottom = new Color(0.13f, 0.08f, 0.02f),
        glow = new Color(1.00f, 0.78f, 0.28f),
        accent = new Color(1.00f, 0.90f, 0.55f),
    };

    [MenuItem("Tools/AirStrike/Generate Leaderboard Bar Art")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SS/Popup", "Leaderboard");
        Save(Bar(TealPalette), Teal);
        Save(Bar(GoldPalette), GoldPath);
        AssetDatabase.Refresh();
        MakeSprite(Teal);
        MakeSprite(GoldPath);
        Debug.Log("[LeaderboardArtGenerator] Bars written to " + Folder);
    }

    static Texture2D Bar(Palette p)
    {
        var px = new Color[W * H];
        const float cut = 14f, outline = 3f, frame = 7f;
        for (int y = 0; y < H; y++)
            for (int x = 0; x < W; x++)
            {
                float d = Chamfered(x + 0.5f, y + 0.5f, cut); // <0 inside
                if (d > 0.5f) { px[y * W + x] = Color.clear; continue; }
                float a = Mathf.Clamp01(0.5f - d);
                float t = (float)y / (H - 1);                  // 0 bottom .. 1 top
                Color c;
                if (d > -outline) c = p.outline;
                else if (d > -outline - frame)
                {
                    // Bevel: bright at the top edge, dark at the bottom edge, rounded profile
                    float k = (-d - outline) / frame;             // 0 outer .. 1 inner
                    float profile = Mathf.Sin(k * Mathf.PI);
                    c = Color.Lerp(p.frameDark, p.frameLight, Mathf.Clamp01(t * 1.1f));
                    c = Color.Lerp(c * 0.75f, c, profile);
                }
                else
                {
                    // Face: vertical gradient, soft glow under the top frame, thin gloss line
                    c = Color.Lerp(p.faceBottom, p.faceTop, Mathf.SmoothStep(0f, 1f, t));
                    float inner = -d - outline - frame;          // distance into the face
                    c += p.glow * 0.22f * Mathf.Clamp01(1f - inner / 18f) * Mathf.Clamp01((t - 0.35f) * 2f);
                    float gloss = Mathf.Clamp01(1f - Mathf.Abs(t - 0.74f) / 0.02f);
                    c = Color.Lerp(c, p.accent, 0.25f * gloss);
                    c *= 0.94f + 0.06f * Noise(x * 0.9f, y * 0.9f);    // faint brushed texture
                    if (inner < 2f) c = Color.Lerp(c, Color.black, 0.45f); // inner shadow line
                }
                c.a = a;
                px[y * W + x] = c;
            }

        // Small accent notches on both ends of the top frame
        for (int i = 0; i < 2; i++)
        {
            int x0 = i == 0 ? 34 : W - 34 - 40;
            for (int y = H - 7; y < H - 4; y++)
                for (int x = x0; x < x0 + 40; x++)
                    px[y * W + x] = Color.Lerp(px[y * W + x], p.accent, 0.8f);
        }

        var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    // Signed distance to a W x H rectangle with 45° cut corners (negative inside)
    static float Chamfered(float x, float y, float c)
    {
        float dx = Mathf.Abs(x - W / 2f) - W / 2f;
        float dy = Mathf.Abs(y - H / 2f) - H / 2f;
        float box = Mathf.Max(dx, dy);
        float corner = (dx + dy + c) / 1.4142f;
        return Mathf.Max(box, corner);
    }

    static float Noise(float x, float y)
    {
        unchecked
        {
            uint h = (uint)((int)x * 374761393 + (int)(y * 0.2f) * 668265263);
            h = (h ^ (h >> 13)) * 1274126177;
            return (h ^ (h >> 16)) / (float)uint.MaxValue;
        }
    }

    static void Save(Texture2D tex, string path)
    {
        File.WriteAllBytes(path, tex.EncodeToPNG());
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
        importer.spriteBorder = new Vector4(Border, Border, Border, Border);
        importer.SaveAndReimport();
    }
}
