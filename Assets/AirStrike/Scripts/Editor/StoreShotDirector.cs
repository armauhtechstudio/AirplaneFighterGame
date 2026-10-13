using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using AirplaneControllerwithShooting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

// Automatic store screenshots. Write a request into StoreScreenshots/request.txt (or use the menu item):
//   capture            all modes          capture classic | mission | openworld   one mode
//   dump <mode>        list what the mode's scene contains in Play Mode (StoreScreenshots/dump.txt)
//   ping / refresh     check the director is alive / reimport changed scripts
// For each mode the director opens the scene, enters Play Mode, stages the shot (player plane in the air,
// enemies in front, an explosion on a target), freezes time, renders it with StoreScreenshotCapture and
// leaves Play Mode. Everything staged exists only in Play Mode: no scene is saved, and the PlayerPrefs it
// sets to pick the mode are restored afterwards. Progress goes to StoreScreenshots/director.log.
[InitializeOnLoad]
public static class StoreShotDirector
{
    static readonly string Root = Path.Combine(Directory.GetParent(Application.dataPath).FullName, "StoreScreenshots");
    static string RequestFile => Path.Combine(Root, "request.txt");
    static string LogFile => Path.Combine(Root, "director.log");
    static string DumpFile => Path.Combine(Root, "dump.txt");

    const string StateKey = "StoreShotDirector.State";
    const float SettleSeconds = 6f; // play time before staging (level setup, spawners)

    const string ClassicScene = "Assets/AirStrike/Demo/Classic.unity";
    const string MissionScene = "Assets/AirStrike/Demo/Mission Low.unity";
    const string ExplosionPrefab = "Assets/AirStrike/Prefabs/Effects/ExplosionBig.prefab";
    const string SmallExplosionPrefab = "Assets/AirStrike/Prefabs/Effects/Explosion.prefab";

    class Step
    {
        public string name;
        public string scene;
        public string[] prefs; // "key=value" set before Play, restored after
        public Action stage;
        public bool manual; // only when asked for by name ("capture art")
    }

    static readonly Step[] Steps =
    {
        new Step { name = "classic", scene = ClassicScene, prefs = new[] { "SelectedMod=1" }, stage = StageClassic },
        new Step { name = "mission", scene = MissionScene, prefs = new[] { "SelectedMod=2" }, stage = StageMission },
        new Step { name = "openworld", scene = MissionScene, prefs = new[] { "SelectedMod=3" }, stage = StageOpenWorld },
        new Step { name = "artjet", scene = MissionScene, prefs = new[] { "SelectedMod=2" }, stage = StageArtJet, manual = true },
        new Step { name = "artclassic", scene = ClassicScene, prefs = new[] { "SelectedMod=1" }, stage = StageArtClassic, manual = true },
    };

    [Serializable]
    class State
    {
        public string phase = "idle"; // idle, open, entering, exiting
        public int step;
        public double phaseStart;
        public double playStart;   // first update seen in Play Mode
        public string originalScene;
        public string only = "";   // run just this step (by name), empty = all
        public bool dump;          // write dump.txt instead of capturing
        public List<string> prefsBackup = new List<string>(); // key|hasKey|value
    }

    static State state;
    static double nextPoll;

    static StoreShotDirector()
    {
        state = JsonUtility.FromJson<State>(SessionState.GetString(StateKey, "{}")) ?? new State();
        EditorApplication.update += Update;
    }

    [MenuItem("Tools/AirStrike/Store Screenshots (automatic)")]
    static void StartFromMenu() => Begin("", false);

    static void Save() => SessionState.SetString(StateKey, JsonUtility.ToJson(state));

    static void Log(string message)
    {
        Directory.CreateDirectory(Root);
        File.AppendAllText(LogFile, $"[{DateTime.Now:HH:mm:ss}] {message}\n");
        Debug.Log("[StoreShotDirector] " + message);
    }

    static void Update()
    {
        double now = EditorApplication.timeSinceStartup;
        if (now < nextPoll) return;
        nextPoll = now + 0.25;

        try
        {
            if (state.phase == "idle") { PollRequest(); return; }
            Advance(now);
        }
        catch (Exception e)
        {
            Log("ERROR: " + e);
            Finish();
        }
    }

    static void PollRequest()
    {
        if (!File.Exists(RequestFile)) return;
        string request = File.ReadAllText(RequestFile).Trim().ToLowerInvariant();
        File.Delete(RequestFile);

        if (request == "refresh") { Log("Refreshing assets"); AssetDatabase.Refresh(); }
        else if (request == "ping") Log("pong (playing=" + EditorApplication.isPlaying + ")");
        else if (request.StartsWith("capture")) Begin(request.Substring(7).Trim(), false);
        else if (request.StartsWith("dump")) Begin(request.Substring(4).Trim(), true);
        else Log("Unknown request: " + request);
    }

    static void Begin(string only, bool dump)
    {
        if (state.phase != "idle") { Log("Already running"); return; }
        if (EditorApplication.isPlayingOrWillChangePlaymode) { Log("ERROR: leave Play Mode first"); return; }
        for (int i = 0; i < EditorSceneManager.sceneCount; i++)
            if (EditorSceneManager.GetSceneAt(i).isDirty)
            {
                Log("ERROR: scene '" + EditorSceneManager.GetSceneAt(i).name + "' has unsaved changes; save it first");
                return;
            }

        state = new State { phase = "open", originalScene = EditorSceneManager.GetActiveScene().path, only = only, dump = dump };
        foreach (Step s in Steps)
            foreach (string kv in s.prefs)
            {
                string key = kv.Split('=')[0];
                if (state.prefsBackup.Exists(b => b.StartsWith(key + "|"))) continue;
                state.prefsBackup.Add($"{key}|{(PlayerPrefs.HasKey(key) ? 1 : 0)}|{PlayerPrefs.GetInt(key, 0)}");
            }
        Save();
        Log((dump ? "Dump" : "Capture") + " started" + (only != "" ? " (" + only + ")" : ""));
    }

    static void Advance(double now)
    {
        switch (state.phase)
        {
            case "open":
                while (state.step < Steps.Length && !Selected(Steps[state.step])) state.step++;
                if (state.step >= Steps.Length) { Finish(); return; }
                Step step = Steps[state.step];
                Log($"Step {step.name}: {step.scene}");
                EditorSceneManager.OpenScene(step.scene, OpenSceneMode.Single);
                foreach (string kv in step.prefs)
                {
                    string[] parts = kv.Split('=');
                    PlayerPrefs.SetInt(parts[0], int.Parse(parts[1]));
                }
                PlayerPrefs.Save();
                state.phase = "entering";
                state.phaseStart = now;
                state.playStart = 0;
                Save();
                EditorApplication.EnterPlaymode();
                break;

            case "entering":
                if (!EditorApplication.isPlaying)
                {
                    if (now - state.phaseStart > 120) throw new Exception("Play Mode did not start (compile errors?)");
                    return;
                }
                if (state.playStart == 0) { state.playStart = now; Save(); }
                if (now - state.playStart < SettleSeconds) return;

                try { if (state.dump) Dump(); else Steps[state.step].stage(); }
                catch (Exception e) { Log("ERROR staging: " + e); }
                finally { Cleanup(); }

                state.phase = "exiting";
                Save();
                EditorApplication.ExitPlaymode();
                break;

            case "exiting":
                if (EditorApplication.isPlayingOrWillChangePlaymode) return;
                state.step++;
                state.phase = "open";
                Save();
                break;
        }
    }

    static bool Selected(Step s) => state.only == "" ? !s.manual : s.name.StartsWith(state.only);

    static void Finish()
    {
        if (EditorApplication.isPlaying) EditorApplication.ExitPlaymode();
        foreach (string b in state.prefsBackup)
        {
            string[] p = b.Split('|');
            if (p[1] == "1") PlayerPrefs.SetInt(p[0], int.Parse(p[2]));
            else PlayerPrefs.DeleteKey(p[0]);
        }
        PlayerPrefs.Save();

        if (!EditorApplication.isPlaying && !string.IsNullOrEmpty(state.originalScene) && File.Exists(state.originalScene))
            EditorSceneManager.OpenScene(state.originalScene, OpenSceneMode.Single);

        bool dump = state.dump;
        state = new State();
        Save();
        Log(dump ? "Dump finished" : "Capture finished");
    }

    // ---------------------------------------------------------------- dump

    static void Dump()
    {
        var sb = new StringBuilder();
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        sb.AppendLine("Scene: " + scene.path);

        Transform player = StoreScreenshotCapture.FindPlayerPlane();
        sb.AppendLine("Player: " + (player ? $"{player.name} at {player.position} radius {PlaneRadius(player):F1}" : "none"));
        Camera cam = StoreScreenshotCapture.FindGameCamera();
        sb.AppendLine("Camera: " + (cam ? $"{cam.name} far {cam.farClipPlane}" : "none"));

        sb.AppendLine("\nRoot objects:");
        foreach (GameObject go in scene.GetRootGameObjects())
            sb.AppendLine($"  {(go.activeSelf ? "+" : "-")} {go.name}  ({go.transform.childCount} children)");

        sb.AppendLine("\nScript types (active / total):");
        var all = UnityEngine.Object.FindObjectsOfType<MonoBehaviour>(true)
            .Where(m => m != null && m.GetType().Namespace == null || (m != null && m.GetType().Namespace.StartsWith("AirplaneController")));
        foreach (var g in all.GroupBy(m => m.GetType().Name).OrderBy(g => g.Key))
        {
            int active = g.Count(m => m.isActiveAndEnabled);
            string names = string.Join(", ", g.Select(m => m.name).Distinct().Take(6));
            sb.AppendLine($"  {g.Key}: {active}/{g.Count()}   [{names}]");
        }

        sb.AppendLine("\nTagged Enemy:");
        try
        {
            foreach (GameObject e in GameObject.FindGameObjectsWithTag("Enemy").Take(40))
                sb.AppendLine($"  {e.name} at {e.transform.position}  [{string.Join(", ", e.GetComponents<Component>().Select(c => c.GetType().Name))}]");
        }
        catch (Exception ex) { sb.AppendLine("  " + ex.Message); }

        GameManager gm = UnityEngine.Object.FindObjectOfType<GameManager>();
        if (gm != null && gm.levels != null)
        {
            sb.AppendLine("\nGameManager levels:");
            foreach (GameObject l in gm.levels)
                if (l != null) sb.AppendLine($"  {(l.activeSelf ? "+" : "-")} {l.name}: {string.Join(", ", l.GetComponentsInChildren<Transform>(true).Where(t => t.parent == l.transform).Select(t => t.name).Take(12))}");
        }

        File.WriteAllText(DumpFile, sb.ToString());
        Log("Wrote dump.txt");
    }

    // ---------------------------------------------------------------- staging

    static readonly List<GameObject> spawned = new List<GameObject>();
    static readonly List<Behaviour> hidden = new List<Behaviour>();

    static string Shot(string name) => Path.Combine(StoreScreenshotCapture.OutputFolder, $"auto_{name}.png");

    static void Capture(string angle)
    {
        HideWorldUI();
        Log(StoreScreenshotCapture.CaptureAngle(StoreScreenshotCapture.FindAngle(angle), Shot(angle)));
        ClearEffects();
    }

    static void Capture(string name, Vector3 camPos, Vector3 look, float fov)
    {
        HideWorldUI();
        StoreScreenshotCapture.Render(StoreScreenshotCapture.FindGameCamera(), camPos,
            Quaternion.LookRotation(look - camPos, Vector3.up), fov, Shot(name));
        Log($"Saved auto_{name}.png");
        ClearEffects();
    }

    // Mode 1: player jet in the air with enemy jets around it
    static void StageClassic()
    {
        Transform player = StoreScreenshotCapture.FindPlayerPlane();
        if (player == null) throw new Exception("no player plane");

        // The tutorial level has no enemy jets: show the last combat level instead
        GameManager gm = GameManager.instance;
        if (gm != null && gm.levels != null && gm.levels.Length > 0)
        {
            foreach (GameObject l in gm.levels) if (l != null) l.SetActive(false);
            gm.levels[gm.levels.Length - 1].SetActive(true);
        }

        List<Transform> enemies = EnemyPlanes(player, 3);
        Log($"Classic: player '{player.name}', {enemies.Count} enemy planes");

        Vector3 start = InTheAir(player.position, 120f);
        Quaternion heading = StoreScreenshotCapture.LevelFrame(player);
        float r = PlaneRadius(player);

        // 1. Chase: enemies spread out ahead, flying away
        Place(player, start, heading * Quaternion.Euler(-4f, 0f, -18f));
        PlaceEnemy(enemies, 0, start + heading * (new Vector3(-3.2f, 1.6f, 8f) * r), heading * Quaternion.Euler(0f, -8f, 25f));
        PlaceEnemy(enemies, 1, start + heading * (new Vector3(3.8f, 0.6f, 11f) * r), heading * Quaternion.Euler(-5f, 10f, -30f));
        PlaceEnemy(enemies, 2, start + heading * (new Vector3(0.6f, 3.2f, 17f) * r), heading * Quaternion.Euler(0f, 3f, 10f));
        Freeze(player, enemies);
        Capture("1_chase", start + heading * new Vector3(-0.6f * r, 0.75f * r, -3.6f * r), start + heading * new Vector3(0f, 0.9f * r, 6f * r), 52f);

        // 2. Front low: hero jet with two enemies chasing behind it
        Place(player, start, heading * Quaternion.Euler(-6f, 0f, 22f));
        PlaceEnemy(enemies, 0, start + heading * (new Vector3(-5f, 2f, -16f) * r), heading * Quaternion.Euler(-4f, 6f, 20f));
        PlaceEnemy(enemies, 1, start + heading * (new Vector3(6f, 3.5f, -26f) * r), heading * Quaternion.Euler(-3f, -4f, -15f));
        HideFrom(enemies, 2);
        Freeze(player, enemies);
        Capture("2_front_low");

        // 5. Target lock: one enemy right ahead, getting hit
        Place(player, start, heading * Quaternion.Euler(-2f, 0f, -12f));
        Vector3 target = start + heading * (new Vector3(2.5f, 1f, 9f) * r);
        PlaceEnemy(enemies, 0, target, heading * Quaternion.Euler(-6f, 12f, 40f));
        PlaceEnemy(enemies, 1, start + heading * (new Vector3(-7f, 3f, 30f) * r), heading * Quaternion.Euler(0f, -10f, -25f));
        HideFrom(enemies, 2);
        Freeze(player, enemies);
        Effect(SmallExplosionPrefab, target + heading * new Vector3(0.3f * r, 0.15f * r, -1.3f * r), 0.2f, 1f);
        Capture("5_target_lock");
    }

    // Mode 2: player jet diving on a group of tanks / houses / balloons, and a low side pass
    static void StageMission()
    {
        Transform player = StoreScreenshotCapture.FindPlayerPlane();
        if (player == null) throw new Exception("no player plane");
        float r = PlaneRadius(player);

        // Show the last (busiest) level: tanks, houses and balloons together
        GameManagerMode2 gm = GameManagerMode2.instance;
        if (gm != null && gm.levels != null && gm.levels.Length > 0)
        {
            foreach (GameObject l in gm.levels) if (l != null) l.SetActive(false);
            gm.levels[gm.levels.Length - 1].SetActive(true);
        }

        List<EnemyAI> targets = GroundTargets();
        Log($"Mission: player '{player.name}', {targets.Count} targets, plane radius {r:F1}");

        // 4. Top down: over the busiest group of targets, a tank exploding
        if (targets.Count > 0)
        {
            Vector3 cluster = BusiestPoint(targets, 90f, out EnemyAI hit);
            EnemyAI tank = targets.Where(t => t.objName == "tank" || t.objName == "house")
                .OrderBy(t => Vector3.Distance(t.transform.position, cluster)).FirstOrDefault();
            if (tank != null) hit = tank;

            Vector3 toCluster = Vector3.ProjectOnPlane(cluster - player.position, Vector3.up);
            Quaternion heading = toCluster.sqrMagnitude > 1f ? Quaternion.LookRotation(toCluster.normalized) : StoreScreenshotCapture.LevelFrame(player);

            Vector3 planePos = InTheAir(hit.transform.position - heading * Vector3.forward * (9f * r), 5f * r, exact: true);
            Place(player, planePos, heading * Quaternion.Euler(22f, 0f, -16f));
            Freeze(player, null);
            Effect(ExplosionPrefab, hit.transform.position + Vector3.up * 1f, 0.25f, 0.8f);

            Vector3 camPos = planePos + heading * new Vector3(1.4f * r, 2.6f * r, -4f * r);
            Capture("4_top_down", camPos, Vector3.Lerp(planePos, hit.transform.position, 0.65f), 60f);
        }
        else Log("WARNING: no ground targets, skipped 4_top_down");

        // 3. Side: banking pass with sky behind the plane
        {
            Quaternion heading = StoreScreenshotCapture.LevelFrame(player);
            Vector3 pos = InTheAir(player.position + heading * Vector3.forward * (12f * r), 14f * r, exact: true);
            Place(player, pos, heading * Quaternion.Euler(-8f, 0f, 16f));
            Freeze(player, null);
            Capture("3_side", pos + heading * new Vector3(4.2f * r, -1.1f * r, 0.6f * r), pos + heading * new Vector3(0f, 0.2f * r, 0.4f * r), 40f);
        }
    }

    // Mode 3: the open world from above and behind the jet, targets spread over the map
    static void StageOpenWorld()
    {
        Transform player = StoreScreenshotCapture.FindPlayerPlane();
        if (player == null) throw new Exception("no player plane");
        float r = PlaneRadius(player);
        List<EnemyAI> targets = GroundTargets();
        Log($"Open world: {targets.Count} targets");

        // Head toward the targets so they fill the background
        Vector3 aim = targets.Count > 0 ? BusiestPoint(targets, 250f, out _) : player.position + player.forward * 200f;
        Vector3 toAim = Vector3.ProjectOnPlane(aim - player.position, Vector3.up);
        Quaternion heading = toAim.sqrMagnitude > 1f ? Quaternion.LookRotation(toAim.normalized) : StoreScreenshotCapture.LevelFrame(player);

        Vector3 pos = InTheAir(aim - heading * Vector3.forward * (28f * r), 9f * r, exact: true);
        Place(player, pos, heading * Quaternion.Euler(3f, 0f, -26f));
        Freeze(player, null);
        Capture("6_wide", pos + heading * new Vector3(-2.4f * r, 1.3f * r, -4.6f * r), pos + heading * new Vector3(0f, -1.6f * r, 10f * r), 58f);
    }

    // ---------------------------------------------------------------- icon / feature graphic art

    static string ArtFile(string name) => Path.Combine(Root, "art", name + ".png");

    // The modern jet as transparent cut-outs, and an explosion on black (for a "screen" blend)
    static void StageArtJet()
    {
        Transform player = StoreScreenshotCapture.FindPlayerPlane();
        if (player == null) throw new Exception("no player plane");
        float r = PlaneRadius(player);
        Quaternion heading = StoreScreenshotCapture.LevelFrame(player);
        Vector3 pos = InTheAir(player.position, 30f * r, exact: true);

        Place(player, pos, heading * Quaternion.Euler(-8f, 0f, 26f));
        Freeze(player, null);
        Vector3 c = StoreScreenshotCapture.PlaneBounds(player).center;
        RenderIsolated(player, c + heading * new Vector3(-1.5f * r, -0.55f * r, 2.7f * r), c, 34f, 2048, 2048, Color.clear, ArtFile("jet_front"));

        Place(player, pos, heading * Quaternion.Euler(-6f, 0f, -24f));
        Freeze(player, null);
        c = StoreScreenshotCapture.PlaneBounds(player).center;
        RenderIsolated(player, c + heading * new Vector3(2.3f * r, 0.5f * r, -2.6f * r), c, 34f, 2048, 2048, Color.clear, ArtFile("jet_rear"));

        // Explosion, framed from its own particle bounds
        Effect(ExplosionPrefab, pos + Vector3.up * 200f, 0.22f, 1f);
        GameObject fx = spawned.Last(go => go != null && go.name == "StoreShotFX");
        Vector3 blast = pos + Vector3.up * 200f;
        Log("Explosion renderers: " + string.Join(", ", fx.GetComponentsInChildren<Renderer>().Select(x => $"{x.name} {x.bounds.size}")));
        RenderIsolated(fx.transform, blast - heading * Vector3.forward * 45f, blast, 40f, 1536, 1536, Color.black, ArtFile("explosion"));
        ClearEffects();
    }

    // The WW2 fighter as a cut-out, and a wide sunset sky with enemy planes for the feature graphic
    static void StageArtClassic()
    {
        Transform player = StoreScreenshotCapture.FindPlayerPlane();
        if (player == null) throw new Exception("no player plane");
        GameManager gm = GameManager.instance;
        if (gm != null && gm.levels != null && gm.levels.Length > 0)
        {
            foreach (GameObject l in gm.levels) if (l != null) l.SetActive(false);
            gm.levels[gm.levels.Length - 1].SetActive(true);
        }
        List<Transform> enemies = EnemyPlanes(player, 3);
        float r = PlaneRadius(player);
        Quaternion heading = StoreScreenshotCapture.LevelFrame(player);
        Vector3 start = InTheAir(player.position, 140f);

        Place(player, start, heading * Quaternion.Euler(-6f, 0f, -24f));
        Freeze(player, enemies);
        HideFrom(enemies, 0);
        Vector3 c = StoreScreenshotCapture.PlaneBounds(player).center;
        RenderIsolated(player, c + heading * new Vector3(1.7f * r, -0.45f * r, 2.6f * r), c, 34f, 2048, 2048, Color.clear, ArtFile("ww2_front"));

        // Background: player out of the way, enemy planes in the distance toward the sunset
        player.position += Vector3.down * 10000f;
        PlaceEnemy(enemies, 0, start + heading * (new Vector3(-2f, 2f, 26f) * r), heading * Quaternion.Euler(-6f, 160f, 30f));
        PlaceEnemy(enemies, 1, start + heading * (new Vector3(4f, 3.5f, 40f) * r), heading * Quaternion.Euler(-4f, 200f, -25f));
        PlaceEnemy(enemies, 2, start + heading * (new Vector3(-7f, 5f, 52f) * r), heading * Quaternion.Euler(-3f, 170f, 15f));
        Freeze(player, enemies);
        HideWorldUI();
        StoreScreenshotCapture.Render(StoreScreenshotCapture.FindGameCamera(), start,
            Quaternion.LookRotation(heading * new Vector3(0f, 0.08f, 1f), Vector3.up), 55f, ArtFile("sky_bg"), 2048, 1000, false);
        Log("Saved art/sky_bg.png");
    }

    // Renders only `root` (moved to an unused layer) over a flat background colour
    static void RenderIsolated(Transform root, Vector3 camPos, Vector3 look, float fov, int w, int h, Color background, string file)
    {
        const int Layer = 31;
        Camera cam = StoreScreenshotCapture.FindGameCamera();
        var layers = new Dictionary<GameObject, int>();
        foreach (Transform t in root.GetComponentsInChildren<Transform>(true)) { layers[t.gameObject] = t.gameObject.layer; t.gameObject.layer = Layer; }
        var effects = cam.GetComponents<Behaviour>().Where(b => b != cam && b.enabled).ToList(); // image effects would wreck the alpha

        int mask = cam.cullingMask;
        CameraClearFlags flags = cam.clearFlags;
        Color bg = cam.backgroundColor;
        try
        {
            foreach (Behaviour b in effects) b.enabled = false;
            cam.cullingMask = 1 << Layer;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = background;
            StoreScreenshotCapture.Render(cam, camPos, Quaternion.LookRotation(look - camPos, Vector3.up), fov, file, w, h, background.a < 1f);
            Log("Saved art/" + Path.GetFileName(file));
        }
        finally
        {
            cam.cullingMask = mask;
            cam.clearFlags = flags;
            cam.backgroundColor = bg;
            foreach (Behaviour b in effects) b.enabled = true;
            foreach (var kv in layers) if (kv.Key != null) kv.Key.layer = kv.Value;
        }
    }

    // ---------------------------------------------------------------- helpers

    static List<EnemyAI> GroundTargets()
    {
        var list = UnityEngine.Object.FindObjectsOfType<EnemyAI>().Where(e => e.Health > 0).ToList();
        foreach (EnemyAI e in list)
            if (e.SpritePointer != null) e.SpritePointer.SetActive(false);
        return list;
    }

    // Classic's enemy planes (AIController, or anything tagged Enemy with a FlightSystem); copies the
    // first one until there are `count`
    static List<Transform> EnemyPlanes(Transform player, int count)
    {
        var list = new List<Transform>();
        foreach (AIController ai in UnityEngine.Object.FindObjectsOfType<AIController>())
            if (!ai.transform.IsChildOf(player)) list.Add(ai.transform);
        if (list.Count == 0)
            foreach (FlightSystem f in UnityEngine.Object.FindObjectsOfType<FlightSystem>())
                if (!f.transform.IsChildOf(player) && !player.IsChildOf(f.transform)) list.Add(f.transform);

        GameObject template = list.Count > 0 ? list[0].gameObject : SpawnerTemplate();
        if (template == null) throw new Exception("no enemy plane to copy");
        while (list.Count < count)
        {
            GameObject copy = UnityEngine.Object.Instantiate(template);
            spawned.Add(copy);
            list.Add(copy.transform);
        }
        return list;
    }

    static float PlaneRadius(Transform plane)
    {
        Bounds b = StoreScreenshotCapture.PlaneBounds(plane);
        return Mathf.Max(b.extents.x, b.extents.z, 1f);
    }

    // p raised to at least `height` above the ground (exact: exactly that height)
    static Vector3 InTheAir(Vector3 p, float height, bool exact = false)
    {
        if (StoreScreenshotCapture.GroundHeight(p, out float ground))
            p.y = exact ? ground + height : Mathf.Max(p.y, ground + height);
        return p;
    }

    static void Place(Transform t, Vector3 pos, Quaternion rot)
    {
        t.gameObject.SetActive(true);
        foreach (Rigidbody rb in t.GetComponentsInChildren<Rigidbody>())
        {
            if (!rb.isKinematic) { rb.velocity = Vector3.zero; rb.angularVelocity = Vector3.zero; }
            rb.isKinematic = true;
        }
        t.SetPositionAndRotation(pos, rot);
    }

    static void PlaceEnemy(List<Transform> enemies, int i, Vector3 pos, Quaternion rot)
    {
        if (i < enemies.Count) Place(enemies[i], pos, rot);
    }

    // Enemies from index `keep` on are moved out of the shot
    static void HideFrom(List<Transform> enemies, int keep)
    {
        for (int i = keep; i < enemies.Count; i++)
            enemies[i].position += Vector3.down * 10000f;
    }

    static void Freeze(Transform player, List<Transform> others)
    {
        Physics.SyncTransforms();
        Time.timeScale = 0f;
        // World-space smoke / trails piled up while the plane was teleported: clear them
        ClearTrails(player);
        if (others != null) foreach (Transform t in others) ClearTrails(t);
    }

    static void ClearTrails(Transform root)
    {
        foreach (TrailRenderer trail in root.GetComponentsInChildren<TrailRenderer>()) trail.Clear();
        foreach (ParticleSystem ps in root.GetComponentsInChildren<ParticleSystem>())
        {
            if (ps.main.simulationSpace == ParticleSystemSimulationSpace.World) ps.Clear(false);
            else { ps.Simulate(1f, false, true); ps.Pause(false); }
        }
    }

    // World-space canvases (test ad boards, labels) don't belong in the shots
    static void HideWorldUI()
    {
        foreach (Canvas c in UnityEngine.Object.FindObjectsOfType<Canvas>())
            if (c.renderMode == RenderMode.WorldSpace && c.enabled)
            {
                c.enabled = false;
                hidden.Add(c);
            }
    }

    // A visual-only copy of an effect prefab (scripts, sounds, colliders and lights stripped so it can't
    // damage anything or light the whole scene), simulated `time` seconds in
    static void Effect(string prefabPath, Vector3 pos, float time, float scale)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        if (prefab == null) { Log("WARNING: missing " + prefabPath); return; }

        var holder = new GameObject("StoreShotFX");
        holder.SetActive(false);
        spawned.Add(holder);
        GameObject fx = UnityEngine.Object.Instantiate(prefab, pos, Quaternion.identity, holder.transform);
        fx.transform.localScale *= scale;
        foreach (MonoBehaviour mb in fx.GetComponentsInChildren<MonoBehaviour>(true)) UnityEngine.Object.DestroyImmediate(mb);
        foreach (AudioSource a in fx.GetComponentsInChildren<AudioSource>(true)) UnityEngine.Object.DestroyImmediate(a);
        foreach (Collider c in fx.GetComponentsInChildren<Collider>(true)) UnityEngine.Object.DestroyImmediate(c);
        foreach (Light l in fx.GetComponentsInChildren<Light>(true)) UnityEngine.Object.DestroyImmediate(l);
        holder.SetActive(true);

        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
        foreach (ParticleSystem ps in fx.GetComponentsInChildren<ParticleSystem>())
            if (ps.transform.parent == null || ps.transform.parent.GetComponentInParent<ParticleSystem>() == null)
            {
                ps.Simulate(time, true, true);
                ps.Pause(true);
            }
    }

    static void ClearEffects()
    {
        foreach (GameObject go in spawned)
            if (go != null && go.name == "StoreShotFX") UnityEngine.Object.Destroy(go);
        spawned.RemoveAll(go => go == null || go.name == "StoreShotFX");
    }

    static void Cleanup()
    {
        foreach (GameObject go in spawned) if (go != null) UnityEngine.Object.Destroy(go);
        spawned.Clear();
        hidden.Clear();
        Time.timeScale = 1f;
    }

    static GameObject SpawnerTemplate()
    {
        // SpawnerEnemy, not SpawnerFriend
        foreach (EnemySpawner s in UnityEngine.Object.FindObjectsOfType<EnemySpawner>(true).OrderBy(s => s.name.Contains("Enemy") ? 0 : 1))
            if (s.Objectman != null && s.Objectman.Length > 0 && s.Objectman[0] != null)
            {
                Log($"Enemy template '{s.Objectman[0].name}' from {s.name}");
                return s.Objectman[0];
            }
        return null;
    }

    // The target with the most other targets within `radius`, and the centre of that group
    static Vector3 BusiestPoint(List<EnemyAI> targets, float radius, out EnemyAI best)
    {
        best = targets[0];
        int bestCount = -1;
        foreach (EnemyAI a in targets)
        {
            int n = targets.Count(b => Vector3.Distance(a.transform.position, b.transform.position) < radius);
            if (n > bestCount) { bestCount = n; best = a; }
        }

        EnemyAI centre = best;
        var group = targets.Where(b => Vector3.Distance(centre.transform.position, b.transform.position) < radius).ToList();
        return group.Aggregate(Vector3.zero, (s, b) => s + b.transform.position) / group.Count;
    }
}
