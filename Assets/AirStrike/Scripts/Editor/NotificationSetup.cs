using System;
using System.IO;
using Unity.Notifications;
using UnityEditor;
using UnityEngine;

// Local notifications setup (see GameNotifications):
//  - Settings: Android "Reschedule on Device Restart" ON (a reboot otherwise drops every pending alarm),
//    exact alarms OFF (Play only allows them for alarm / calendar apps), iOS "Request Authorization on
//    App Launch" OFF (asked from the first level win instead).
//  - Small icon "icon_small": a white plane silhouette on transparent (Android tints the alpha and
//    shows a normal icon as a grey square), Read/Write enabled, uncompressed.
public static class NotificationSetup
{
    const string IconPath = "Assets/AirStrike/Notifications/icon_small.png";

    [MenuItem("Tools/AirStrike/Notifications/Setup (settings + icon)")]
    public static void Setup() => Debug.Log("[NotificationSetup] " + Run());

    public static string Run()
    {
        NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
        NotificationSettings.AndroidSettings.ExactSchedulingOption = 0;
        NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;

        if (!AssetDatabase.IsValidFolder("Assets/AirStrike/Notifications")) AssetDatabase.CreateFolder("Assets/AirStrike", "Notifications");
        File.WriteAllBytes(IconPath, PlaneSilhouette(96).EncodeToPNG());
        AssetDatabase.ImportAsset(IconPath);
        var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
        importer.textureType = TextureImporterType.Default;
        importer.isReadable = true;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();

        var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
        NotificationSettings.AndroidSettings.RemoveDrawableResource(GameNotifications.SmallIconId);
        NotificationSettings.AndroidSettings.AddDrawableResource(GameNotifications.SmallIconId, icon, NotificationIconType.Small);

        return $"Reschedule on restart ON, exact alarms OFF, iOS auth on launch OFF; small icon \"{GameNotifications.SmallIconId}\" = {IconPath}";
    }

    [MenuItem("Tools/AirStrike/Notifications/Dump Schedule")]
    public static void Dump()
    {
        var now = DateTime.Now;
        string text = $"Leaving at {now:g}:\n";
        foreach (var r in GameNotifications.BuildSchedule(now, new System.Random()))
            text += $"  +{(r.fireTime - now).TotalHours:0}h  {r.fireTime:ddd HH:mm}  \"{r.title}\" - {r.body}\n";
        Debug.Log("[Notifications] " + text);
    }

    // White jet seen from above, pointing up, on transparent
    static Texture2D PlaneSilhouette(int size)
    {
        Vector2[] shape =
        {
            new Vector2(0.50f, 0.96f), // nose
            new Vector2(0.56f, 0.80f), new Vector2(0.57f, 0.60f),
            new Vector2(0.94f, 0.42f), new Vector2(0.94f, 0.34f), // right wing
            new Vector2(0.57f, 0.42f), new Vector2(0.56f, 0.20f),
            new Vector2(0.74f, 0.10f), new Vector2(0.74f, 0.04f), // right tail
            new Vector2(0.50f, 0.09f),
            new Vector2(0.26f, 0.04f), new Vector2(0.26f, 0.10f), // left tail
            new Vector2(0.44f, 0.20f), new Vector2(0.43f, 0.42f),
            new Vector2(0.06f, 0.34f), new Vector2(0.06f, 0.42f), // left wing
            new Vector2(0.43f, 0.60f), new Vector2(0.44f, 0.80f),
        };
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var px = new Color[size * size];
        const int ss = 4;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int hits = 0;
                for (int sy = 0; sy < ss; sy++)
                    for (int sx = 0; sx < ss; sx++)
                        if (Inside(new Vector2((x + (sx + 0.5f) / ss) / size, (y + (sy + 0.5f) / ss) / size), shape)) hits++;
                px[y * size + x] = new Color(1f, 1f, 1f, hits / (float)(ss * ss));
            }
        tex.SetPixels(px);
        tex.Apply();
        return tex;
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
}
