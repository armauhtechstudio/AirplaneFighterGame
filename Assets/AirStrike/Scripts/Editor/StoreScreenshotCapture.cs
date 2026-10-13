using System;
using System.Collections.Generic;
using System.IO;
using AirplaneControllerwithShooting;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

// Store screenshots: in Play Mode, renders the game camera from 6 cinematic angles around the player's
// plane into <project>/StoreScreenshots/raw (1920x1080, rendered 2x and downscaled by make_banners.py).
// Only the 3D world is rendered (overlay UI/HUD is left out), so the banners have a clean background.
// Tip: press Pause (Ctrl+Shift+P) at a good moment (enemies close, explosions), then Capture.
// StoreShotDirector uses the same angles to stage and capture the shots automatically.
public class StoreScreenshotCapture : EditorWindow
{
    public const int Width = 1920, Height = 1080;

    // Offsets/look points are in plane radii, in a level frame (plane heading, world up), so the
    // horizon stays straight while the plane itself banks.
    public struct Angle
    {
        public string name;
        public Vector3 offset;    // camera position: x = right, y = up, z = forward
        public Vector3 look;      // look-at point relative to the plane
        public float fov;
        public bool towardEnemy;  // frame the nearest enemy behind the plane when there is one

        public Angle(string name, Vector3 offset, Vector3 look, float fov, bool towardEnemy = false)
        {
            this.name = name; this.offset = offset; this.look = look; this.fov = fov; this.towardEnemy = towardEnemy;
        }
    }

    public static readonly Angle[] Angles =
    {
        new Angle("1_chase",       new Vector3( 0.0f,  0.9f, -4.2f), new Vector3(0f, 0.2f, 4f),   50f),
        new Angle("2_front_low",   new Vector3( 2.2f, -0.7f,  3.6f), new Vector3(0f, 0f, 0.3f),   42f),
        new Angle("3_side",        new Vector3( 4.6f,  0.3f,  0.4f), new Vector3(0f, 0f, 0.4f),   38f),
        new Angle("4_top_down",    new Vector3( 1.2f,  7.0f, -2.5f), new Vector3(0f, 0f, 1.5f),   50f),
        new Angle("5_target_lock", new Vector3(-1.6f,  0.5f, -3.4f), new Vector3(0f, 0f, 10f),    45f, true),
        new Angle("6_wide",        new Vector3(-9.0f,  2.5f,  9.0f), new Vector3(0f, 0f, 1.5f),   55f),
    };

    [SerializeField] int supersample = 2;
    [SerializeField] bool includeOtherCameras = false;
    string status = "";

    public static string OutputFolder => Path.Combine(Directory.GetParent(Application.dataPath).FullName, "StoreScreenshots", "raw");

    [MenuItem("Tools/AirStrike/Store Screenshots")]
    static void Open() => GetWindow<StoreScreenshotCapture>("Store Screenshots");

    void OnGUI()
    {
        EditorGUILayout.HelpBox(
            "1. Enter Play Mode in a gameplay scene and fly.\n" +
            "2. Press Pause at a good moment (enemies / targets in view).\n" +
            "3. Capture all 6 angles, or one at a time.\n" +
            "Shots go to StoreScreenshots/raw. Run make_banners.py to add the feature banners.",
            MessageType.Info);

        supersample = EditorGUILayout.IntSlider("Supersample", supersample, 1, 3);
        includeOtherCameras = EditorGUILayout.Toggle(new GUIContent("Include other cameras",
            "Also render other enabled cameras (e.g. a world-space UI or effects camera) on top of the main one."), includeOtherCameras);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Capture all 6 angles", GUILayout.Height(36)))
            {
                var log = new System.Text.StringBuilder();
                foreach (Angle a in Angles) log.AppendLine(CaptureAngle(a, null, supersample, includeOtherCameras));
                status = log.ToString().TrimEnd();
            }

            EditorGUILayout.Space();
            foreach (Angle a in Angles)
                if (GUILayout.Button("Capture " + a.name))
                    status = CaptureAngle(a, null, supersample, includeOtherCameras);
        }

        if (!Application.isPlaying)
            EditorGUILayout.HelpBox("Enter Play Mode to capture.", MessageType.Warning);

        if (GUILayout.Button("Open output folder"))
        {
            Directory.CreateDirectory(OutputFolder);
            EditorUtility.RevealInFinder(OutputFolder);
        }

        if (!string.IsNullOrEmpty(status))
            EditorGUILayout.HelpBox(status, MessageType.None);
    }

    public static Angle FindAngle(string name) => Array.Find(Angles, a => a.name == name);

    /// <summary>Renders one angle around the player's plane. file null = scene + angle + time.</summary>
    public static string CaptureAngle(Angle angle, string file, int supersample = 2, bool includeOtherCameras = false)
    {
        Camera cam = FindGameCamera();
        Transform plane = FindPlayerPlane();
        if (cam == null) return "ERROR: no enabled game camera found.";
        if (plane == null) return "ERROR: no player plane found (PlayerManager / AirplaneController).";

        Bounds bounds = PlaneBounds(plane);
        float r = Mathf.Max(bounds.extents.x, bounds.extents.z, 1f);
        Vector3 center = bounds.center;

        // Level frame: plane heading flattened on the ground plane, world up
        Quaternion frame = LevelFrame(plane);

        Vector3 look = center + frame * (angle.look * r);
        if (angle.towardEnemy)
        {
            Transform enemy = NearestEnemy(plane, center);
            if (enemy != null)
            {
                // Swing the frame so the enemy sits ahead of the plane, and look between the two
                Vector3 toEnemy = Vector3.ProjectOnPlane(enemy.position - center, Vector3.up);
                if (toEnemy.sqrMagnitude > 1f) frame = Quaternion.LookRotation(toEnemy.normalized, Vector3.up);
                look = Vector3.Lerp(center, enemy.position, 0.35f);
            }
        }

        Vector3 pos = SafeCameraPosition(center, frame, angle.offset * r, plane);

        if (file == null)
        {
            string sceneName = SceneManager.GetActiveScene().name.Replace(' ', '_');
            file = Path.Combine(OutputFolder, $"{sceneName}_{angle.name}_{DateTime.Now:HHmmss}.png");
        }
        Render(cam, pos, Quaternion.LookRotation(look - pos, Vector3.up), angle.fov, file, supersample, includeOtherCameras);
        return "Saved " + Path.GetFileName(file);
    }

    public static Quaternion LevelFrame(Transform plane)
    {
        Vector3 fwd = Vector3.ProjectOnPlane(plane.forward, Vector3.up);
        if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.ProjectOnPlane(plane.up, Vector3.up);
        return Quaternion.LookRotation(fwd.normalized, Vector3.up);
    }

    // Keeps the camera above the ground and with a clear line of sight to the plane (mirrors the shot
    // to the other side, then pulls it in, when something is in the way)
    public static Vector3 SafeCameraPosition(Vector3 center, Quaternion frame, Vector3 offset, Transform plane)
    {
        Vector3[] tries =
        {
            center + frame * offset,
            center + frame * new Vector3(-offset.x, offset.y, offset.z),
            center + frame * (offset * 0.6f),
            center + frame * new Vector3(offset.x * 0.6f, Mathf.Abs(offset.y) * 0.6f + 1f, offset.z * 0.6f),
        };

        foreach (Vector3 t in tries)
        {
            Vector3 p = AboveGround(t);
            if (!Blocked(p, center, plane)) return p;
        }
        return AboveGround(tries[0]);
    }

    public static bool GroundHeight(Vector3 p, out float height)
    {
        // Highest static surface under p (planes and other rigidbodies don't count)
        bool found = false;
        height = float.MinValue;
        foreach (RaycastHit hit in Physics.RaycastAll(new Vector3(p.x, 5000f, p.z), Vector3.down, 10000f, ~0, QueryTriggerInteraction.Ignore))
            if (hit.rigidbody == null && hit.point.y > height)
            {
                height = hit.point.y;
                found = true;
            }
        if (!found) height = 0f;
        return found;
    }

    static Vector3 AboveGround(Vector3 p)
    {
        if (GroundHeight(p, out float ground) && ground > p.y - 3f)
            p.y = ground + 3f;
        return p;
    }

    static bool Blocked(Vector3 from, Vector3 to, Transform plane)
    {
        foreach (RaycastHit hit in Physics.RaycastAll(from, to - from, Vector3.Distance(from, to), ~0, QueryTriggerInteraction.Ignore))
            if (!hit.transform.IsChildOf(plane) && hit.rigidbody == null) return true;
        return false;
    }

    public static void Render(Camera cam, Vector3 pos, Quaternion rot, float fov, string file, int supersample = 2, bool includeOtherCameras = false) =>
        Render(cam, pos, rot, fov, file, Width * supersample, Height * supersample, false, includeOtherCameras);

    /// <summary>Renders w x h pixels; alpha keeps the camera's background alpha (for cut-outs).</summary>
    public static void Render(Camera cam, Vector3 pos, Quaternion rot, float fov, string file, int w, int h, bool alpha, bool includeOtherCameras = false)
    {
        var rt = new RenderTexture(w, h, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default) { antiAliasing = 8 };
        var tex = new Texture2D(w, h, alpha ? TextureFormat.RGBA32 : TextureFormat.RGB24, false);

        var cams = new List<Camera> { cam };
        if (includeOtherCameras)
            foreach (Camera c in Camera.allCameras)
                if (c != cam && c.targetTexture == null) cams.Add(c);
        cams.Sort((a, b) => a.depth.CompareTo(b.depth));

        Vector3 savedPos = cam.transform.position;
        Quaternion savedRot = cam.transform.rotation;
        float savedFov = cam.fieldOfView;
        try
        {
            cam.transform.SetPositionAndRotation(pos, rot);
            cam.fieldOfView = fov;
            foreach (Camera c in cams)
            {
                RenderTexture savedTarget = c.targetTexture;
                c.targetTexture = rt;
                c.aspect = (float)w / h;
                c.Render();
                c.targetTexture = savedTarget;
                c.ResetAspect();
            }

            RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, w, h), 0, 0);
            tex.Apply();
            Directory.CreateDirectory(Path.GetDirectoryName(file));
            File.WriteAllBytes(file, tex.EncodeToPNG());
        }
        finally
        {
            RenderTexture.active = null;
            cam.transform.SetPositionAndRotation(savedPos, savedRot);
            cam.fieldOfView = savedFov;
            rt.Release();
            DestroyImmediate(rt);
            DestroyImmediate(tex);
        }
    }

    public static Camera FindGameCamera()
    {
        if (Camera.main != null && Camera.main.isActiveAndEnabled) return Camera.main;
        Camera best = null;
        foreach (Camera c in Camera.allCameras)
            if (c.targetTexture == null && (best == null || c.depth > best.depth)) best = c;
        return best;
    }

    public static Transform FindPlayerPlane()
    {
        // Classic uses PlayerManager (+ FlightView), Mission Low / Open World use AirplaneController
        var classic = FindObjectOfType<PlayerManager>();
        if (classic != null) return classic.transform;
        if (AirplaneController.Instance != null) return AirplaneController.Instance.transform;
        var controller = FindObjectOfType<AirplaneController>();
        return controller != null ? controller.transform : null;
    }

    public static Transform NearestEnemy(Transform plane, Vector3 from)
    {
        var enemies = new List<Transform>();
        foreach (var e in FindObjectsOfType<AIController>()) enemies.Add(e.transform);
        foreach (var e in FindObjectsOfType<EnemyAI>()) enemies.Add(e.transform);

        Transform best = null;
        float bestDist = float.MaxValue;
        foreach (Transform e in enemies)
        {
            if (e.IsChildOf(plane) || !e.gameObject.activeInHierarchy) continue;
            float d = Vector3.Distance(from, e.position);
            if (d < bestDist) { bestDist = d; best = e; }
        }
        return best;
    }

    // Bounds of the plane's meshes only (not its particles, trails, sprites or UI)
    public static Bounds PlaneBounds(Transform root)
    {
        Bounds b = new Bounds(root.position, Vector3.one);
        bool first = true;
        foreach (Renderer rend in root.GetComponentsInChildren<Renderer>())
        {
            if (!(rend is MeshRenderer || rend is SkinnedMeshRenderer)) continue;
            if (first) { b = rend.bounds; first = false; }
            else b.Encapsulate(rend.bounds);
        }
        return b;
    }
}
