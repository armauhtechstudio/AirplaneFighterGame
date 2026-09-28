using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;

// Builds Mission Low levels 4..10 by cloning the already-working targets from Level1 (balloons),
// Level2 (tanks) and Level3 (houses) and mixing them with increasing counts.
// Safe to re-run: levels 4..10 are cleared and rebuilt, levels 1..3 are never touched.
public static class MissionLowLevelsBuilder
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";

    struct LevelPlan
    {
        public int balloons, tanks, houses;
        public LevelPlan(int b, int t, int h) { balloons = b; tanks = t; houses = h; }
    }

    // Index 0..2 are the existing hand-made levels (kept as they are)
    static readonly LevelPlan[] Plan =
    {
        new LevelPlan(2, 0, 0), // Level 1  (existing)
        new LevelPlan(0, 2, 0), // Level 2  (existing)
        new LevelPlan(0, 0, 3), // Level 3  (existing)
        new LevelPlan(3, 2, 0), // Level 4
        new LevelPlan(2, 0, 4), // Level 5
        new LevelPlan(0, 4, 2), // Level 6
        new LevelPlan(4, 3, 2), // Level 7
        new LevelPlan(5, 0, 4), // Level 8
        new LevelPlan(3, 5, 4), // Level 9
        new LevelPlan(6, 5, 5), // Level 10
    };
    const int FirstGeneratedLevel = 3;

    // Placement tuning for extra targets around the original spots
    const float MinRingDistance = 60f;
    const float MaxRingDistance = 180f;
    const float MinSpacing = 35f;          // between targets of the same level
    const float MinDistanceFromPool = 30f; // from the free-standing enemies/buildings already in the scene
    const float TimerSecondsPerTarget = 30f;

    [MenuItem("Tools/AirStrike/Build Mission Low Levels 4-10")]
    public static void RunFromMenu()
    {
        Run();
    }

    // Called via: Unity.exe -batchmode -nographics -quit -projectPath <proj> -executeMethod MissionLowLevelsBuilder.RunFromCLI
    public static void RunFromCLI()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Run();
        EditorSceneManager.SaveScene(EditorSceneManager.GetActiveScene());
    }

    static void Run()
    {
        var log = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        log.AppendLine($"Scene: {scene.name} ({scene.path})");

        var gm = Object.FindObjectOfType<GameManagerMode2>(true);
        if (gm == null)
        {
            log.AppendLine("ERROR: No GameManagerMode2 found — open the Mission Low scene first.");
            Flush(log);
            return;
        }

        Transform levelsRoot = FindInScene(scene, "Levels");
        Transform level1 = levelsRoot != null ? levelsRoot.Find("Level1") : null;
        Transform level2 = levelsRoot != null ? levelsRoot.Find("Level2") : null;
        Transform level3 = levelsRoot != null ? levelsRoot.Find("Level3") : null;
        if (level1 == null || level2 == null || level3 == null)
        {
            log.AppendLine("ERROR: Need Levels/Level1, Level2 and Level3 — run \"Diagnose And Fix Mission Low\" first.");
            Flush(log);
            return;
        }

        List<Transform> balloonTemplates = Children(level1);
        List<Transform> tankTemplates = Children(level2);
        List<Transform> houseTemplates = Children(level3);
        if (balloonTemplates.Count == 0 || tankTemplates.Count == 0 || houseTemplates.Count == 0)
        {
            log.AppendLine($"ERROR: Level1/2/3 must each have at least one target (balloons={balloonTemplates.Count}, tanks={tankTemplates.Count}, houses={houseTemplates.Count}).");
            Flush(log);
            return;
        }

        // Fix the templates first so every clone inherits correct scoring + one HUD marker each
        SetEnemyAIObjName(balloonTemplates, "balloon", log);
        SetEnemyAIObjName(tankTemplates, "tank", log); // was empty, which scored tanks as generators
        MissionLowHUDSetup.RefreshMarkers(levelsRoot, log);

        // Spots already used by the free-standing enemies/buildings, so clones don't overlap them
        var pool = new List<Vector3>();
        foreach (string groupName in new[] { "Enemies_Air", "Enemies_Land", "Collapsibles" })
        {
            Transform group = FindInScene(scene, groupName);
            if (group != null)
                foreach (Transform t in Children(group)) pool.Add(t.position);
        }

        Physics.SyncTransforms();

        // ---- Build the level GameObjects ----
        var levelObjects = new GameObject[Plan.Length];
        levelObjects[0] = level1.gameObject;
        levelObjects[1] = level2.gameObject;
        levelObjects[2] = level3.gameObject;

        for (int i = FirstGeneratedLevel; i < Plan.Length; i++)
        {
            Transform level = GetOrCreateChild(levelsRoot, $"Level{i + 1}");
            foreach (Transform old in Children(level))
                Undo.DestroyObjectImmediate(old.gameObject);

            var rng = new System.Random(1000 + i); // deterministic so re-runs give the same layout
            var used = new List<Vector3>();
            LevelPlan p = Plan[i];

            Spawn(level, balloonTemplates, p.balloons, "Balloon", false, rng, used, pool, log);
            Spawn(level, tankTemplates, p.tanks, "Tank", true, rng, used, pool, log);
            Spawn(level, houseTemplates, p.houses, "House", true, rng, used, pool, log);

            level.gameObject.SetActive(false);
            levelObjects[i] = level.gameObject;
            log.AppendLine($"Level{i + 1}: {p.balloons} balloons, {p.tanks} tanks, {p.houses} houses");
        }

        MissionLowHUDSetup.RefreshMarkers(levelsRoot, log);

        // ---- GameManagerMode2 arrays ----
        var so = new SerializedObject(gm);

        var levelsProp = so.FindProperty("levels");
        levelsProp.arraySize = Plan.Length;
        for (int i = 0; i < Plan.Length; i++)
            levelsProp.GetArrayElementAtIndex(i).objectReferenceValue = levelObjects[i];

        var reqProp = so.FindProperty("destroyRequirementsMode2");
        int oldSize = reqProp.arraySize;
        // Growing the array copies the last existing element (Level 3), which keeps its spawn point / timer setup
        reqProp.arraySize = Plan.Length;

        for (int i = FirstGeneratedLevel; i < Plan.Length; i++)
        {
            SerializedProperty req = reqProp.GetArrayElementAtIndex(i);
            SerializedProperty baseReq = reqProp.GetArrayElementAtIndex(FirstGeneratedLevel - 1);
            LevelPlan p = Plan[i];

            req.FindPropertyRelative("planesT").intValue = 0;
            req.FindPropertyRelative("generatorsT").intValue = 0;
            req.FindPropertyRelative("balloonsT").intValue = p.balloons;
            req.FindPropertyRelative("tanksT").intValue = p.tanks;
            req.FindPropertyRelative("housesT").intValue = p.houses;
            req.FindPropertyRelative("objective").stringValue = ""; // auto-built from the counts at runtime
            req.FindPropertyRelative("hasCargo").boolValue = false;
            req.FindPropertyRelative("isTutorial").boolValue = false;
            req.FindPropertyRelative("playerSpawnPoint").objectReferenceValue =
                baseReq.FindPropertyRelative("playerSpawnPoint").objectReferenceValue;

            bool useTimer = baseReq.FindPropertyRelative("useTimer").boolValue;
            req.FindPropertyRelative("useTimer").boolValue = useTimer;
            if (useTimer)
            {
                float baseTime = baseReq.FindPropertyRelative("timeLimit").floatValue;
                int targets = p.balloons + p.tanks + p.houses;
                req.FindPropertyRelative("timeLimit").floatValue = Mathf.Max(baseTime, targets * TimerSecondsPerTarget);
            }
        }
        so.ApplyModifiedProperties();
        log.AppendLine($"GameManagerMode2: levels[] and destroyRequirementsMode2[] set to {Plan.Length} (requirements was {oldSize}).");

        EditorSceneManager.MarkSceneDirty(scene);
        log.AppendLine("Done. Save the scene (Ctrl+S). The main menu's Mod 2 level list (mod2Levels) needs 10 buttons too.");
        Flush(log);
    }

    static void SetEnemyAIObjName(List<Transform> targets, string objName, StringBuilder log)
    {
        foreach (Transform t in targets)
        {
            foreach (var ai in t.GetComponentsInChildren<AirplaneControllerwithShooting.EnemyAI>(true))
            {
                if (ai.objName == objName) continue;
                Undo.RecordObject(ai, "Set EnemyAI objName");
                log.AppendLine($"FIXED: EnemyAI \"{ai.name}\" objName \"{ai.objName}\" -> \"{objName}\"");
                ai.objName = objName;
                EditorUtility.SetDirty(ai);
            }
        }
    }

    static void Spawn(Transform level, List<Transform> templates, int count, string label, bool onGround,
                      System.Random rng, List<Vector3> used, List<Vector3> pool, StringBuilder log)
    {
        // Use the original spots first (other levels are inactive, so no overlap), then spread out around them
        var anchors = new List<Transform>(templates);
        Shuffle(anchors, rng);

        for (int n = 0; n < count; n++)
        {
            Transform template = anchors[n % anchors.Count];
            Vector3 pos = n < anchors.Count ? template.position : FindSpot(template, onGround, rng, used, pool, log);

            GameObject clone = Object.Instantiate(template.gameObject, pos, template.rotation, level);
            clone.name = $"{label}_{n + 1}";
            clone.SetActive(true);
            Undo.RegisterCreatedObjectUndo(clone, $"Create {clone.name}");
            used.Add(pos);
        }
    }

    internal static Vector3 FindSpot(Transform anchor, bool onGround, System.Random rng, List<Vector3> used, List<Vector3> pool, StringBuilder log)
    {
        Vector3 best = anchor.position;
        for (int attempt = 0; attempt < 200; attempt++)
        {
            float angle = (float)(rng.NextDouble() * Mathf.PI * 2f);
            float dist = Mathf.Lerp(MinRingDistance, MaxRingDistance, (float)rng.NextDouble());
            Vector3 pos = anchor.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * dist;

            if (onGround)
            {
                if (!SnapToGround(anchor, ref pos)) continue;
            }
            else
            {
                pos.y += Mathf.Lerp(-10f, 15f, (float)rng.NextDouble());
            }

            best = pos;
            if (IsClear(pos, used, MinSpacing) && IsClear(pos, pool, MinDistanceFromPool))
                return pos;
        }
        log.AppendLine($"  WARNING: couldn't find a clear spot near \"{anchor.name}\"; placed it as close as possible — check it in the Scene view.");
        return best;
    }

    // Keeps the template's height above the ground and puts NavMesh units back onto the NavMesh.
    static bool SnapToGround(Transform anchor, ref Vector3 pos)
    {
        float? anchorGround = GroundHeight(anchor.position, anchor);
        float? targetGround = GroundHeight(pos, anchor);
        if (anchorGround == null || targetGround == null) return false;
        pos.y = targetGround.Value + (anchor.position.y - anchorGround.Value);

        if (anchor.GetComponentInChildren<NavMeshAgent>(true) != null)
        {
            if (!NavMesh.SamplePosition(pos, out NavMeshHit hit, 30f, NavMesh.AllAreas)) return false;
            pos = hit.position;
        }
        return true;
    }

    static float? GroundHeight(Vector3 at, Transform ignore)
    {
        foreach (Terrain terrain in Terrain.activeTerrains)
        {
            Vector3 tp = terrain.transform.position;
            Vector3 size = terrain.terrainData.size;
            if (at.x >= tp.x && at.x <= tp.x + size.x && at.z >= tp.z && at.z <= tp.z + size.z)
                return terrain.SampleHeight(at) + tp.y;
        }

        foreach (RaycastHit hit in Physics.RaycastAll(at + Vector3.up * 1000f, Vector3.down, 3000f, ~0, QueryTriggerInteraction.Ignore))
        {
            if (hit.transform.IsChildOf(ignore)) continue;
            return hit.point.y;
        }
        return null;
    }

    internal static bool IsClear(Vector3 pos, List<Vector3> others, float minDistance)
    {
        foreach (Vector3 o in others)
        {
            Vector2 a = new Vector2(pos.x, pos.z), b = new Vector2(o.x, o.z);
            if (Vector2.Distance(a, b) < minDistance) return false;
        }
        return true;
    }

    internal static void Shuffle<T>(List<T> list, System.Random rng)
    {
        for (int i = list.Count - 1; i > 0; i--)
        {
            int j = rng.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }
    }

    static List<Transform> Children(Transform parent)
    {
        var list = new List<Transform>();
        for (int i = 0; i < parent.childCount; i++) list.Add(parent.GetChild(i));
        return list;
    }

    static Transform GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null) return existing;

        GameObject go = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(go, $"Create {name}");
        Undo.SetTransformParent(go.transform, parent, $"Parent {name}");
        go.transform.localPosition = Vector3.zero;
        return go.transform;
    }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }

    static void Flush(StringBuilder log)
    {
        Debug.Log("[MissionLowLevelsBuilder]\n" + log);
    }
}
