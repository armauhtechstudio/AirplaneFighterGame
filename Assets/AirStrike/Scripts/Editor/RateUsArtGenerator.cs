using System.IO;
using UnityEditor;
using UnityEngine;

// Generates the Rate Us stars into Assets/SS/Popup/RateUs:
//  - star_empty: dark steel star with a dark outline (not rated)
//  - star_gold:  glossy gold star with a dark outline (rated)
public static class RateUsArtGenerator
{
    public const string Folder = "Assets/SS/Popup/RateUs";

    [MenuItem("Tools/AirStrike/Generate Rate Us Art")]
    public static void Generate()
    {
        if (!AssetDatabase.IsValidFolder(Folder)) AssetDatabase.CreateFolder("Assets/SS/Popup", "RateUs");

        Save(Star(256, new Color(0.16f, 0.17f, 0.19f), new Color(0.42f, 0.44f, 0.47f), 0.10f), "star_empty.png");
        Save(Star(256, new Color(0.80f, 0.45f, 0.04f), new Color(1f, 0.92f, 0.45f), 0.40f), "star_gold.png");

        AssetDatabase.Refresh();
        foreach (string file in Directory.GetFiles(Folder, "*.png")) MakeSprite(file.Replace('\\', '/'));
        Debug.Log("[RateUsArtGenerator] Rate Us art written to " + Folder);
    }

    // 5-point star, tip up: dark outline, body shaded bottom -> top, gloss on the upper-left
    static Texture2D Star(int size, Color bottom, Color top, float gloss)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        Vector2[] outer = StarPoints(size, 0.47f), inner = StarPoints(size, 0.40f);
        const int ss = 3;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float edge = 0f, fill = 0f;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                    {
                        var p = new Vector2(x + (sx + 0.5f) / ss, y + (sy + 0.5f) / ss);
                        if (Inside(p, outer)) edge += 1f;
                        if (Inside(p, inner)) fill += 1f;
                    }
                edge /= ss * ss;
                fill /= ss * ss;
                if (edge <= 0f) { px[y * size + x] = Color.clear; continue; }

                float up = (float)y / size;
                Color body = Color.Lerp(bottom, top, Mathf.SmoothStep(0.15f, 0.85f, up));
                float gx = (x - size * 0.40f) / (size * 0.16f), gy = (y - size * 0.62f) / (size * 0.10f);
                body = Color.Lerp(body, Color.white, Mathf.Clamp01(1f - (gx * gx + gy * gy)) * gloss);
                Color col = Color.Lerp(new Color(0.07f, 0.05f, 0.03f), body, fill);
                col.a = edge;
                px[y * size + x] = col;
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
    }

    static Vector2[] StarPoints(int size, float radius)
    {
        var pts = new Vector2[10];
        Vector2 c = new Vector2(size / 2f, size * 0.48f);
        for (int i = 0; i < 10; i++)
        {
            float r = (i % 2 == 0 ? radius : radius * 0.45f) * size;
            float a = Mathf.PI / 2f + i * Mathf.PI / 5f;
            pts[i] = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
        }
        return pts;
    }

    static bool Inside(Vector2 p, Vector2[] poly)
    {
        bool inside = false;
        for (int i = 0, j = poly.Length - 1; i < poly.Length; j = i++)
            if ((poly[i].y > p.y) != (poly[j].y > p.y) &&
                p.x < (poly[j].x - poly[i].x) * (p.y - poly[i].y) / (poly[j].y - poly[i].y) + poly[i].x)
                inside = !inside;
        return inside;
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
