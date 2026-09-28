using System.Text;
using SickscoreGames.HUDNavigationSystem;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

// Adds HUD Navigation System markers to every target (balloons / tanks / houses) in the Mission Low
// scene so the player can see where the enemies are, including off-screen arrows.
// The HUD System + HUD Navigation Canvas are copied from the Classic scene so they look the same.
public static class MissionLowHUDSetup
{
    const string ScenePath = "Assets/AirStrike/Demo/Mission Low.unity";
    const string ClassicScenePath = "Assets/AirStrike/Demo/Classic.unity";
    const string CanvasPrefabPath = "Assets/Sickscore Games/HUD-Navigation-System/Resources/Prefabs/HUD Navigation Canvas.prefab";

    // Prefabs whose HUDNavigationElement is used as the marker template (first one found wins)
    static readonly string[] TemplatePrefabPaths =
    {
        "Assets/AirStrike/Prefabs/Fighter/WW2AI.prefab",
        "Assets/AirStrike/Prefabs/Weapons/Sentry/Generator.prefab",
        "Assets/AirStrike/Prefabs/Weapons/Sentry/CargoEnemy.prefab",
    };

    const string LevelsName = "Levels";

    [MenuItem("Tools/AirStrike/Add Enemy HUD Navigation To Mission Low")]
    public static void RunFromMenu()
    {
        if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
        Run();
    }

    // Called via: Unity.exe -batchmode -nographics -quit -projectPath <proj> -executeMethod MissionLowHUDSetup.RunFromCLI
    public static void RunFromCLI()
    {
        EditorSceneManager.OpenScene(ScenePath);
        Run();
        EditorSceneManager.SaveScene(SceneManager.GetSceneByPath(ScenePath));
    }

    static void Run()
    {
        var log = new StringBuilder();
        Scene scene = SceneManager.GetActiveScene();
        log.AppendLine($"Scene: {scene.name} ({scene.path})");

        if (Object.FindObjectOfType<GameManagerMode2>(true) == null)
        {
            log.AppendLine("ERROR: No GameManagerMode2 found — open the Mission Low scene first.");
            Flush(log);
            return;
        }

        // ---- 1. HUD System + HUD Navigation Canvas ----
        HUDNavigationSystem system = Object.FindObjectOfType<HUDNavigationSystem>(true);
        HUDNavigationCanvas canvas = Object.FindObjectOfType<HUDNavigationCanvas>(true);
        if (system == null || canvas == null)
            CopyHUDFromClassic(scene, ref system, ref canvas, log);

        if (canvas == null)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(CanvasPrefabPath);
            if (prefab != null)
            {
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, scene);
                Undo.RegisterCreatedObjectUndo(go, "Add HUD Navigation Canvas");
                canvas = go.GetComponentInChildren<HUDNavigationCanvas>(true);
                log.AppendLine("Added HUD Navigation Canvas from the asset's default prefab.");
            }
        }
        if (system == null)
        {
            var go = new GameObject("HUD System");
            SceneManager.MoveGameObjectToScene(go, scene);
            Undo.RegisterCreatedObjectUndo(go, "Add HUD System");
            system = go.AddComponent<HUDNavigationSystem>();
            log.AppendLine("Added a new HUD System with default settings.");
        }
        if (canvas == null)
        {
            log.AppendLine("ERROR: Couldn't create a HUD Navigation Canvas.");
            Flush(log);
            return;
        }

        ConfigureSystem(system, log);

        // ---- 2. Markers on every target ----
        Transform levels = FindInScene(scene, LevelsName);
        if (levels == null)
        {
            log.AppendLine($"ERROR: Couldn't find \"{LevelsName}\" in the scene.");
            Flush(log);
            return;
        }
        RefreshMarkers(levels, log);

        EditorSceneManager.MarkSceneDirty(scene);
        log.AppendLine("Done. Save the scene (Ctrl+S) to keep the changes.");
        Flush(log);
    }

    // Copies the configured HUD System / HUD Navigation Canvas GameObjects out of the Classic scene.
    static void CopyHUDFromClassic(Scene target, ref HUDNavigationSystem system, ref HUDNavigationCanvas canvas, StringBuilder log)
    {
        Scene classic = EditorSceneManager.OpenScene(ClassicScenePath, OpenSceneMode.Additive);
        try
        {
            foreach (GameObject root in classic.GetRootGameObjects())
            {
                if (system == null)
                {
                    var src = root.GetComponentInChildren<HUDNavigationSystem>(true);
                    if (src != null)
                    {
                        // Copy only the component: in Classic it may sit on a bigger object we don't want to duplicate
                        var go = new GameObject("HUD System");
                        SceneManager.MoveGameObjectToScene(go, target);
                        Undo.RegisterCreatedObjectUndo(go, "Add HUD System");
                        system = go.AddComponent<HUDNavigationSystem>();
                        EditorUtility.CopySerialized(src, system);
                        system.NavigationElements?.Clear(); // those point at Classic's objects
                        log.AppendLine($"Copied HUD System settings from Classic (\"{src.gameObject.name}\").");
                    }
                }
                if (canvas == null)
                {
                    var src = root.GetComponentInChildren<HUDNavigationCanvas>(true);
                    if (src != null && src.GetComponentInParent<PlayerController>(true) == null)
                    {
                        canvas = CopyInto(src.gameObject, target).GetComponent<HUDNavigationCanvas>();
                        log.AppendLine($"Copied \"{src.gameObject.name}\" (HUD Navigation Canvas) from Classic.");
                    }
                }
            }
        }
        finally
        {
            EditorSceneManager.CloseScene(classic, true);
            SceneManager.SetActiveScene(target);
        }
    }

    static GameObject CopyInto(GameObject source, Scene target)
    {
        GameObject copy = Object.Instantiate(source);
        copy.name = source.name;
        SceneManager.MoveGameObjectToScene(copy, target);
        Undo.RegisterCreatedObjectUndo(copy, $"Copy {source.name}");
        return copy;
    }

    // Points the system at this scene's player + flight camera. The minimap is turned off because
    // map profiles are baked per-scene and Classic's would be wrong here.
    static void ConfigureSystem(HUDNavigationSystem system, StringBuilder log)
    {
        Undo.RecordObject(system, "Configure HUD System");

        FlightView view = Object.FindObjectOfType<FlightView>(true);
        Camera cam = view != null ? view.GetComponent<Camera>() : Camera.main;
        // Mission Low flies the AirplaneControllerwithShooting plane; other scenes use PlayerController
        Transform player = null;
        var airplane = Object.FindObjectOfType<AirplaneControllerwithShooting.AirplaneController>(true);
        if (airplane != null) player = airplane.transform;
        else
        {
            var pc = Object.FindObjectOfType<PlayerController>(true);
            if (pc != null) player = pc.transform;
        }

        system.PlayerCamera = cam;
        system.PlayerController = player;
        system.useIndicators = true;
        system.useOffscreenIndicators = true;
        system.useMinimap = false;

        log.AppendLine($"HUD System: PlayerCamera={(cam != null ? cam.name : "NULL")}, PlayerController={(player != null ? player.name : "NULL")}, indicators ON, minimap OFF.");
        if (cam == null || player == null)
            log.AppendLine("WARNING: Assign the missing PlayerCamera / PlayerController on the HUD System manually.");

        EditorUtility.SetDirty(system);
    }

    // Gives every target under Levels exactly one marker:
    //  - houses (many DamageManager pieces) get a HouseTarget + one marker on the house root,
    //    and any old per-piece markers are removed
    //  - EnemyAI targets get a marker on the EnemyAI object (EnemyAI.GetDamage hides it on death)
    //  - other DamageManager targets get a marker on the same object, so it disappears when
    //    DamageManager.Dead() destroys that GameObject
    public static void RefreshMarkers(Transform levels, StringBuilder log)
    {
        HUDNavigationElement template = LoadTemplate(log);
        if (template == null)
        {
            log.AppendLine("ERROR: No HUDNavigationElement template found on the enemy prefabs.");
            return;
        }

        EnsureHouseTargets(levels, log);

        int added = 0, existing = 0, removed = 0;

        foreach (HouseTarget house in levels.GetComponentsInChildren<HouseTarget>(true))
        {
            AddMarker(house.gameObject, house.objName, template, log, ref added, ref existing);
            foreach (HUDNavigationElement piece in house.GetComponentsInChildren<HUDNavigationElement>(true))
            {
                if (piece.gameObject == house.gameObject) continue;
                Undo.DestroyObjectImmediate(piece);
                removed++;
            }
        }

        foreach (var ai in levels.GetComponentsInChildren<AirplaneControllerwithShooting.EnemyAI>(true))
            AddMarker(ai.gameObject, ai.objName, template, log, ref added, ref existing);

        foreach (DamageManager dm in levels.GetComponentsInChildren<DamageManager>(true))
        {
            if (dm.isPlayer) continue;
            if (dm.GetComponentInParent<AirplaneControllerwithShooting.EnemyAI>(true) != null) continue;
            if (dm.GetComponentInParent<HouseTarget>(true) != null) continue;
            AddMarker(dm.gameObject, dm.objName, template, log, ref added, ref existing);
        }

        log.AppendLine($"Markers: {added} added, {existing} already present, {removed} per-piece house markers removed.");
        if (added + existing == 0)
            log.AppendLine("WARNING: No targets found under Levels.");
    }

    // A level target (direct child of LevelN) whose pieces are DamageManagers named "house"
    // becomes one HouseTarget so it scores once instead of once per broken piece.
    static void EnsureHouseTargets(Transform levels, StringBuilder log)
    {
        foreach (Transform level in levels)
        {
            foreach (Transform target in level)
            {
                if (target.GetComponent<HouseTarget>() != null) continue;

                bool isHouse = false;
                foreach (DamageManager dm in target.GetComponentsInChildren<DamageManager>(true))
                    if (dm.objName == "house") { isHouse = true; break; }
                if (!isHouse) continue;

                Undo.AddComponent<HouseTarget>(target.gameObject);
                log.AppendLine($"  + HouseTarget on {GetPath(target)}");
            }
        }
    }

    static void AddMarker(GameObject go, string objName, HUDNavigationElement template, StringBuilder log, ref int added, ref int existing)
    {
        if (go.GetComponent<HUDNavigationElement>() != null)
        {
            existing++;
            return;
        }

        HUDNavigationElement element = Undo.AddComponent<HUDNavigationElement>(go);
        EditorUtility.CopySerialized(template, element);
        added++;
        log.AppendLine($"  + marker on {GetPath(go.transform)} (objName={objName})");
    }

    static HUDNavigationElement LoadTemplate(StringBuilder log)
    {
        foreach (string path in TemplatePrefabPaths)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) continue;
            var element = prefab.GetComponentInChildren<HUDNavigationElement>(true);
            if (element == null) continue;
            log.AppendLine($"Marker template: HUDNavigationElement from {path}");
            return element;
        }
        return null;
    }

    static Transform FindInScene(Scene scene, string name)
    {
        foreach (GameObject root in scene.GetRootGameObjects())
            foreach (Transform t in root.GetComponentsInChildren<Transform>(true))
                if (t.name == name) return t;
        return null;
    }

    static string GetPath(Transform t)
    {
        string path = t.name;
        for (Transform p = t.parent; p != null; p = p.parent)
            path = p.name + "/" + path;
        return path;
    }

    static void Flush(StringBuilder log)
    {
        Debug.Log("[MissionLowHUDSetup]\n" + log);
    }
}
