using System.Collections.Generic;
using System.Linq;
using System.Text;
using AirplaneControllerwithShooting;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Sets up Mode 3 (Open World) in Mission Low:
//  - "OpenWorld" (inactive until Mode 3 is played) with an OpenWorldManager, target templates and spawn points
//  - a Score panel on the gameplay canvas
//  - GameManagerMode2 references, the main menu's Mode 3 scene name and the build settings entry
// Safe to re-run: OpenWorld and ScorePanel are rebuilt.
public static class MissionLowOpenWorldSetup
{
    const string MissionLowPath = "Assets/AirStrike/Demo/Mission Low.unity";
    const string MainMenuPath = "Assets/AirStrike/Demo/Mainmenu.unity";

    const int BalloonsAlive = 6, TanksAlive = 8, HousesAlive = 10;
    // More spawn points than targets so respawns show up in different places
    const int BalloonPoints = 10, TankPoints = 12, HousePoints = 15;
    const float PointSpacing = 35f, PoolSpacing = 30f;

    [MenuItem("Tools/AirStrike/Setup Open World (Mode 3)")]
    public static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        RunFromCLI();
    }

    public static void RunFromCLI()
    {
        var log = new StringBuilder();

        EditorSceneManager.OpenScene(MissionLowPath);
        if (SetupMissionLow(log))
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        EditorSceneManager.OpenScene(MainMenuPath);
        if (SetupMainMenu(log))
            EditorSceneManager.SaveScene(SceneManager.GetActiveScene());

        EnsureInBuildSettings(MissionLowPath, log);

        EditorSceneManager.OpenScene(MissionLowPath);
        Debug.Log("[MissionLowOpenWorldSetup]\n" + log);
    }

    static bool SetupMissionLow(StringBuilder log)
    {
        Scene scene = SceneManager.GetActiveScene();
        var gm = Object.FindObjectOfType<GameManagerMode2>(true);
        Transform levels = FindInScene(scene, "Levels");
        if (gm == null || levels == null)
        {
            log.AppendLine("ERROR: Mission Low needs GameManagerMode2 and Levels.");
            return false;
        }

        // ---- Existing level targets by type (templates + known-good spots) ----
        var balloons = new List<Transform>();
        var tanks = new List<Transform>();
        var houses = new List<Transform>();
        foreach (Transform level in levels)
        {
            foreach (Transform target in level)
            {
                if (target.GetComponent<HouseTarget>() != null) { houses.Add(target); continue; }
                EnemyAI ai = target.GetComponent<EnemyAI>();
                if (ai == null) continue;
                if (ai.objName == "balloon") balloons.Add(target);
                else if (ai.objName == "tank") tanks.Add(target);
            }
        }
        log.AppendLine($"Level targets found: {balloons.Count} balloons, {tanks.Count} tanks, {houses.Count} houses.");
        if (balloons.Count == 0 || tanks.Count == 0 || houses.Count == 0)
        {
            log.AppendLine("ERROR: Need at least one balloon, tank and house under Levels (run the level tools first).");
            return false;
        }

        // ---- Rebuild OpenWorld ----
        Transform old = FindInScene(scene, "OpenWorld");
        if (old != null) Object.DestroyImmediate(old.gameObject);

        var root = new GameObject("OpenWorld");
        SceneManager.MoveGameObjectToScene(root, scene);

        var templatesRoot = new GameObject("Templates").transform;
        templatesRoot.SetParent(root.transform, false);
        templatesRoot.gameObject.SetActive(false); // templates never run themselves

        GameObject balloonTemplate = MakeTemplate(balloons[0], templatesRoot, "BalloonTemplate");
        GameObject tankTemplate = MakeTemplate(tanks[0], templatesRoot, "TankTemplate");
        GameObject houseTemplate = MakeTemplate(houses[0], templatesRoot, "BuildingTemplate");

        var pool = new List<Vector3>();
        foreach (string groupName in new[] { "Enemies_Air", "Enemies_Land", "Collapsibles" })
        {
            Transform group = FindInScene(scene, groupName);
            if (group != null) foreach (Transform t in group) pool.Add(t.position);
        }

        Physics.SyncTransforms();
        var rng = new System.Random(3003);
        var airUsed = new List<Vector3>();
        var groundUsed = new List<Vector3>(); // tanks and buildings must not overlap each other

        Transform balloonPoints = MakePoints(root.transform, "BalloonSpawnPoints", balloons, BalloonPoints, false, rng, airUsed, pool, log);
        Transform housePoints = MakePoints(root.transform, "BuildingSpawnPoints", houses, HousePoints, true, rng, groundUsed, pool, log);
        Transform tankPoints = MakePoints(root.transform, "TankSpawnPoints", tanks, TankPoints, true, rng, groundUsed, pool, log);

        var manager = root.AddComponent<OpenWorldManager>();
        manager.respawnDelay = 12f;
        manager.groups = new[]
        {
            new OpenWorldManager.SpawnGroup { label = "Balloon", template = balloonTemplate, spawnPointsRoot = balloonPoints, maxAlive = BalloonsAlive },
            new OpenWorldManager.SpawnGroup { label = "Tank", template = tankTemplate, spawnPointsRoot = tankPoints, maxAlive = TanksAlive },
            new OpenWorldManager.SpawnGroup { label = "Building", template = houseTemplate, spawnPointsRoot = housePoints, maxAlive = HousesAlive },
        };
        root.SetActive(false); // GameManagerMode2 turns it on only in Mode 3
        log.AppendLine($"OpenWorld: {BalloonsAlive} balloons / {TanksAlive} tanks / {HousesAlive} buildings alive, respawn 12s.");

        // ---- Score panel ----
        Text reference = gm.housesText != null ? gm.housesText : gm.balloonsText;
        if (reference == null)
        {
            log.AppendLine("ERROR: GameManagerMode2 has no housesText/balloonsText to find the gameplay canvas.");
            return false;
        }
        Canvas canvas = reference.canvas.rootCanvas;
        Transform oldPanel = canvas.transform.Find("ScorePanel");
        if (oldPanel != null) Object.DestroyImmediate(oldPanel.gameObject);

        GameObject panel = CreateScorePanel(canvas.transform, reference.font, out Text scoreText, out Text bestText);
        log.AppendLine($"ScorePanel created on canvas \"{canvas.name}\" (top-centre).");

        // ---- GameManagerMode2 references ----
        var so = new SerializedObject(gm);
        so.FindProperty("openWorldRoot").objectReferenceValue = root;
        so.FindProperty("scorePanel").objectReferenceValue = panel;
        so.FindProperty("scoreText").objectReferenceValue = scoreText;
        so.FindProperty("bestScoreText").objectReferenceValue = bestText;
        so.ApplyModifiedProperties();

        EditorSceneManager.MarkSceneDirty(scene);
        return true;
    }

    // ---------------------------------------------------------------------------------------------
    // Scatter: spread the Open World spawn points over the WHOLE (expanded) terrain, the same number
    // in each of a 3x3 grid of areas, away from trees/rocks, the runway and each other.
    // ---------------------------------------------------------------------------------------------

    const int ScatterGrid = 3;
    const int BalloonPointsPerArea = 3, TankPointsPerArea = 3, BuildingPointsPerArea = 3;

    [MenuItem("Tools/AirStrike/Scatter Open World Spawn Points (whole map)")]
    public static void ScatterFromMenu()
    {
        if (SceneManager.GetActiveScene().path != MissionLowPath)
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            EditorSceneManager.OpenScene(MissionLowPath);
        }
        Debug.Log("[MissionLowOpenWorldSetup]\n" + Scatter(SceneManager.GetActiveScene()));
    }

    public static string Scatter(Scene scene)
    {
        var log = new StringBuilder();
        Transform openWorld = FindInScene(scene, "OpenWorld");
        if (openWorld == null) return "ERROR: OpenWorld not found — run Setup Open World (Mode 3) first.";

        Transform balloonRoot = openWorld.Find("BalloonSpawnPoints");
        Transform tankRoot = openWorld.Find("TankSpawnPoints");
        Transform buildingRoot = openWorld.Find("BuildingSpawnPoints");
        if (balloonRoot == null || tankRoot == null || buildingRoot == null) return "ERROR: spawn point groups missing under OpenWorld.";

        // World = every terrain in the scene (original + expanded tiles)
        Bounds world = default;
        bool any = false;
        foreach (Terrain t in Object.FindObjectsOfType<Terrain>(true))
        {
            if (t.gameObject.scene != scene) continue;
            var b = new Bounds(t.transform.position + t.terrainData.size * 0.5f, t.terrainData.size);
            if (!any) { world = b; any = true; } else world.Encapsulate(b);
        }
        if (!any) return "ERROR: no Terrain in the scene.";
        log.AppendLine($"World: x {world.min.x:0}..{world.max.x:0}, z {world.min.z:0}..{world.max.z:0}");

        // Keep each type at the height it already spawns at (ground offset / balloon altitude)
        float balloonMinY = float.MaxValue, balloonMaxY = float.MinValue;
        foreach (Transform p in balloonRoot) { balloonMinY = Mathf.Min(balloonMinY, p.position.y); balloonMaxY = Mathf.Max(balloonMaxY, p.position.y); }
        if (balloonMinY > balloonMaxY) { balloonMinY = 5f; balloonMaxY = 5f; }
        float tankOffset = GroundOffset(tankRoot);
        float buildingOffset = GroundOffset(buildingRoot);

        Physics.SyncTransforms();
        var rng = new System.Random(7070);
        var groundUsed = new List<Vector3>();
        var airUsed = new List<Vector3>();
        Vector3 runway = Vector3.zero;
        Transform road = FindInScene(scene, "Road_Airport");
        if (road != null) runway = road.position;

        var buildings = PlacePoints(world, BuildingPointsPerArea, rng, groundUsed, 180f, p =>
            ClearOfRunway(p, runway, 200f) && ClearOfColliders(p, 26f, openWorld), p => p + Vector3.up * buildingOffset);
        var tanks = PlacePoints(world, TankPointsPerArea, rng, groundUsed, 140f, p =>
            ClearOfRunway(p, runway, 150f) && ClearOfColliders(p, 10f, openWorld), p => p + Vector3.up * tankOffset);
        var balloons = PlacePoints(world, BalloonPointsPerArea, rng, airUsed, 220f, p => true,
            p => new Vector3(p.x, Mathf.Lerp(balloonMinY, balloonMaxY, (float)rng.NextDouble()), p.z));

        ReplacePoints(buildingRoot, buildings);
        ReplacePoints(tankRoot, tanks);
        ReplacePoints(balloonRoot, balloons);
        log.AppendLine($"Spawn points: {buildings.Count} buildings, {tanks.Count} tanks, {balloons.Count} balloons spread over {ScatterGrid}x{ScatterGrid} areas.");

        int inCentre = 0, total = 0;
        foreach (Vector3 p in tanks) { total++; if (UnityEngine.AI.NavMesh.SamplePosition(p, out _, 5f, UnityEngine.AI.NavMesh.AllAreas)) inCentre++; }
        log.AppendLine($"Tanks on the NavMesh (can patrol): {inCentre}/{total}; the rest hold position and shoot.");

        EditorSceneManager.MarkSceneDirty(scene);
        return log.ToString();
    }

    // Points evenly spread: the same number inside each cell of a 3x3 grid over the world
    static List<Vector3> PlacePoints(Bounds world, int perArea, System.Random rng, List<Vector3> used, float spacing,
                                     System.Func<Vector3, bool> isClear, System.Func<Vector3, Vector3> toFinal)
    {
        var result = new List<Vector3>();
        const float margin = 80f;
        float cellX = world.size.x / ScatterGrid, cellZ = world.size.z / ScatterGrid;
        for (int cx = 0; cx < ScatterGrid; cx++)
        {
            for (int cz = 0; cz < ScatterGrid; cz++)
            {
                int placed = 0;
                for (int attempt = 0; attempt < 400 && placed < perArea; attempt++)
                {
                    float x = world.min.x + cx * cellX + Mathf.Lerp(margin, cellX - margin, (float)rng.NextDouble());
                    float z = world.min.z + cz * cellZ + Mathf.Lerp(margin, cellZ - margin, (float)rng.NextDouble());
                    Vector3 ground = new Vector3(x, GroundY(x, z), z);
                    float needSpacing = attempt < 300 ? spacing : spacing * 0.5f; // relax if crowded
                    if (!MissionLowLevelsBuilder.IsClear(ground, used, needSpacing) || !isClear(ground)) continue;
                    used.Add(ground);
                    result.Add(toFinal(ground));
                    placed++;
                }
            }
        }
        return result;
    }

    static float GroundY(float x, float z)
    {
        foreach (Terrain t in Terrain.activeTerrains)
        {
            Vector3 p = t.transform.position, s = t.terrainData.size;
            if (x >= p.x && x <= p.x + s.x && z >= p.z && z <= p.z + s.z)
                return t.SampleHeight(new Vector3(x, 0f, z)) + p.y;
        }
        return 0f;
    }

    // Average height of existing points above the ground (e.g. a building's pivot offset)
    static float GroundOffset(Transform points)
    {
        float sum = 0f;
        int n = 0;
        foreach (Transform p in points) { sum += p.position.y - GroundY(p.position.x, p.position.z); n++; }
        return n > 0 ? sum / n : 0f;
    }

    static bool ClearOfRunway(Vector3 p, Vector3 runway, float radius)
    {
        return new Vector2(p.x - runway.x, p.z - runway.z).magnitude > radius;
    }

    // Nothing solid (trees, rocks, other buildings) within radius, ignoring the ground itself
    static bool ClearOfColliders(Vector3 p, float radius, Transform openWorld)
    {
        foreach (Collider c in Physics.OverlapSphere(p + Vector3.up * (radius * 0.5f), radius, ~0, QueryTriggerInteraction.Ignore))
        {
            if (c is TerrainCollider) continue;
            if (c.transform.IsChildOf(openWorld)) continue;
            return false;
        }
        return true;
    }

    static void ReplacePoints(Transform root, List<Vector3> points)
    {
        for (int i = root.childCount - 1; i >= 0; i--)
            Object.DestroyImmediate(root.GetChild(i).gameObject);
        var rng = new System.Random(root.name.GetHashCode());
        for (int i = 0; i < points.Count; i++)
        {
            var p = new GameObject($"Point_{i + 1}").transform;
            p.SetParent(root, false);
            p.SetPositionAndRotation(points[i], Quaternion.Euler(0f, (float)(rng.NextDouble() * 360.0), 0f));
        }
    }

    static GameObject MakeTemplate(Transform source, Transform parent, string name)
    {
        GameObject t = Object.Instantiate(source.gameObject, parent);
        t.name = name;
        t.transform.localPosition = Vector3.zero;
        t.SetActive(true); // inactive through its parent; clones spawn active
        return t;
    }

    // Spawn points: first the spots the levels already use (known to be valid), then new spots around them
    static Transform MakePoints(Transform root, string name, List<Transform> targets, int count, bool onGround,
                                System.Random rng, List<Vector3> used, List<Vector3> pool, StringBuilder log)
    {
        var parent = new GameObject(name).transform;
        parent.SetParent(root, false);

        var candidates = new List<Transform>(targets);
        MissionLowLevelsBuilder.Shuffle(candidates, rng);

        var points = new List<(Vector3 pos, Quaternion rot)>();
        foreach (Transform t in candidates)
        {
            if (points.Count >= count) break;
            if (!MissionLowLevelsBuilder.IsClear(t.position, used, PointSpacing)) continue;
            if (!MissionLowLevelsBuilder.IsClear(t.position, pool, PoolSpacing)) continue;
            points.Add((t.position, t.rotation));
            used.Add(t.position);
        }
        int reused = points.Count;

        for (int guard = 0; points.Count < count && guard < count * 4; guard++)
        {
            Transform anchor = targets[rng.Next(targets.Count)];
            Vector3 pos = MissionLowLevelsBuilder.FindSpot(anchor, onGround, rng, used, pool, log);
            if (!MissionLowLevelsBuilder.IsClear(pos, used, PointSpacing)) continue;
            points.Add((pos, anchor.rotation));
            used.Add(pos);
        }

        for (int i = 0; i < points.Count; i++)
        {
            var p = new GameObject($"Point_{i + 1}").transform;
            p.SetParent(parent, false);
            p.SetPositionAndRotation(points[i].pos, points[i].rot);
        }

        log.AppendLine($"{name}: {points.Count} points ({reused} from level spots, {points.Count - reused} new).");
        if (points.Count < count)
            log.AppendLine($"  WARNING: wanted {count}; add more points under {name} by hand if needed.");
        return parent;
    }

    static GameObject CreateScorePanel(Transform canvas, Font font, out Text scoreText, out Text bestText)
    {
        var panel = new GameObject("ScorePanel", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)panel.transform;
        rt.SetParent(canvas, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -20f);
        rt.sizeDelta = new Vector2(380f, 110f);
        panel.GetComponent<Image>().color = new Color(0f, 0f, 0f, 0.45f);
        panel.GetComponent<Image>().raycastTarget = false;

        scoreText = CreateText(rt, "ScoreText", font, 44, Color.white, new Vector2(0f, 0.4f), new Vector2(1f, 1f), "Score: 0");
        bestText = CreateText(rt, "BestScoreText", font, 28, new Color(1f, 0.85f, 0.2f), new Vector2(0f, 0f), new Vector2(1f, 0.4f), "Best: 0");

        panel.SetActive(false); // GameManagerMode2 shows it only in Mode 3
        return panel;
    }

    static Text CreateText(RectTransform parent, string name, Font font, int size, Color color, Vector2 anchorMin, Vector2 anchorMax, string value)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline));
        var rt = (RectTransform)go.transform;
        rt.SetParent(parent, false);
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.offsetMin = rt.offsetMax = Vector2.zero;

        var text = go.GetComponent<Text>();
        text.font = font;
        text.fontSize = size;
        text.fontStyle = FontStyle.Bold;
        text.color = color;
        text.alignment = TextAnchor.MiddleCenter;
        text.raycastTarget = false;
        text.text = value;
        go.GetComponent<Outline>().effectColor = new Color(0f, 0f, 0f, 0.6f);
        return text;
    }

    // Mode 3 in the main menu loads the same scene as Mode 2 (Mission Low)
    static bool SetupMainMenu(StringBuilder log)
    {
        var menu = Object.FindObjectOfType<MainMenuController>(true);
        if (menu == null)
        {
            log.AppendLine("ERROR: No MainMenuController in Mainmenu.");
            return false;
        }

        var so = new SerializedObject(menu);
        string mod2Scene = so.FindProperty("mod2SceneName").stringValue;
        string sceneName = System.IO.Path.GetFileNameWithoutExtension(MissionLowPath);
        so.FindProperty("mod3SceneName").stringValue = sceneName;
        so.ApplyModifiedProperties();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        log.AppendLine($"Main menu: mod3SceneName = \"{sceneName}\" (mod2SceneName is \"{mod2Scene}\").");
        return true;
    }

    static void EnsureInBuildSettings(string path, StringBuilder log)
    {
        var scenes = EditorBuildSettings.scenes.ToList();
        var entry = scenes.FirstOrDefault(s => s.path == path);
        if (entry != null && entry.enabled)
        {
            log.AppendLine($"Build settings: \"{path}\" already included.");
            return;
        }
        if (entry != null) entry.enabled = true;
        else scenes.Add(new EditorBuildSettingsScene(path, true));
        EditorBuildSettings.scenes = scenes.ToArray();
        log.AppendLine($"Build settings: added/enabled \"{path}\".");
    }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }
}
